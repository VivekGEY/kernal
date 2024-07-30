# Copyright (c) Microsoft. All rights reserved.


import asyncio
import logging
import sys
from collections.abc import AsyncGenerator
from typing import TYPE_CHECKING, Any

import google.generativeai as genai
from google.generativeai import GenerativeModel
from google.generativeai.protos import Candidate, Content
from google.generativeai.types import AsyncGenerateContentResponse, GenerateContentResponse, GenerationConfig
from pydantic import ValidationError

from semantic_kernel.connectors.ai.function_choice_behavior import FunctionChoiceBehavior
from semantic_kernel.connectors.ai.google.google_ai.google_ai_prompt_execution_settings import (
    GoogleAIChatPromptExecutionSettings,
)
from semantic_kernel.connectors.ai.google.google_ai.services.google_ai_base import GoogleAIBase
from semantic_kernel.connectors.ai.google.google_ai.services.utils import (
    filter_system_message,
    finish_reason_from_google_ai_to_semantic_kernel,
    format_assistant_message,
    format_gemini_function_name_to_kernel_function_fully_qualified_name,
    format_tool_message,
    format_user_message,
    update_settings_from_function_choice_configuration,
)
from semantic_kernel.contents.function_call_content import FunctionCallContent
from semantic_kernel.contents.streaming_chat_message_content import StreamingChatMessageContent
from semantic_kernel.contents.text_content import TextContent
from semantic_kernel.contents.utils.author_role import AuthorRole
from semantic_kernel.contents.utils.finish_reason import FinishReason
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.kernel import Kernel

if sys.version_info >= (3, 12):
    from typing import override  # pragma: no cover
else:
    from typing_extensions import override  # pragma: no cover

from semantic_kernel.connectors.ai.chat_completion_client_base import ChatCompletionClientBase
from semantic_kernel.connectors.ai.google.google_ai.google_ai_settings import GoogleAISettings
from semantic_kernel.contents.chat_history import ChatHistory
from semantic_kernel.contents.chat_message_content import ITEM_TYPES, ChatMessageContent
from semantic_kernel.exceptions.service_exceptions import (
    ServiceInitializationError,
    ServiceInvalidExecutionSettingsError,
)

if TYPE_CHECKING:
    from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings

logger: logging.Logger = logging.getLogger(__name__)


class GoogleAIChatCompletion(GoogleAIBase, ChatCompletionClientBase):
    """Google AI Chat Completion Client."""

    def __init__(
        self,
        gemini_model_id: str | None = None,
        api_key: str | None = None,
        service_id: str | None = None,
        env_file_path: str | None = None,
        env_file_encoding: str | None = None,
    ) -> None:
        """Initialize the Google AI Chat Completion Client.

        If no arguments are provided, the service will attempt to load the settings from the environment.
        The following environment variables are used:
        - GOOGLE_AI_GEMINI_MODEL_ID
        - GOOGLE_AI_API_KEY

        Args:
            gemini_model_id (str | None): The Gemini model ID. (Optional)
            api_key (str | None): The API key. (Optional)
            service_id (str | None): The service ID. (Optional)
            env_file_path (str | None): The path to the .env file. (Optional)
            env_file_encoding (str | None): The encoding of the .env file. (Optional)

        Raises:
            ServiceInitializationError: If an error occurs during initialization.
        """
        try:
            google_ai_settings = GoogleAISettings.create(
                gemini_model_id=gemini_model_id,
                api_key=api_key,
                env_file_path=env_file_path,
                env_file_encoding=env_file_encoding,
            )
        except ValidationError as e:
            raise ServiceInitializationError(f"Failed to validate Google AI settings: {e}") from e
        if not google_ai_settings.gemini_model_id:
            raise ServiceInitializationError("The Google AI Gemini model ID is required.")

        super().__init__(
            ai_model_id=google_ai_settings.gemini_model_id,
            service_id=service_id or google_ai_settings.gemini_model_id,
            service_settings=google_ai_settings,
        )

    # region Non-streaming
    @override
    async def get_chat_message_contents(
        self,
        chat_history: ChatHistory,
        settings: "PromptExecutionSettings",
        **kwargs: Any,
    ) -> list[ChatMessageContent]:
        settings = self.get_prompt_execution_settings_from_settings(settings)
        assert isinstance(settings, GoogleAIChatPromptExecutionSettings)  # nosec

        if (
            settings.function_choice_behavior is None
            or not settings.function_choice_behavior.auto_invoke_kernel_functions
        ):
            return await self._send_chat_request(chat_history, settings)

        kernel = kwargs.get("kernel")
        if not isinstance(kernel, Kernel):
            raise ServiceInvalidExecutionSettingsError("Kernel is required for auto invoking functions.")

        self._configure_function_choice_behavior(settings, kernel)

        for request_index in range(settings.function_choice_behavior.maximum_auto_invoke_attempts):
            completions = await self._send_chat_request(chat_history, settings)
            chat_history.add_message(message=completions[0])
            function_calls = [item for item in chat_history.messages[-1].items if isinstance(item, FunctionCallContent)]
            if (fc_count := len(function_calls)) == 0:
                return completions

            results = await self._invoke_function_calls(
                function_calls=function_calls,
                chat_history=chat_history,
                kernel=kernel,
                arguments=kwargs.get("argument", None),
                function_call_count=fc_count,
                request_index=request_index,
                function_behavior=settings.function_choice_behavior,
            )

            if any(result.terminate for result in results if result is not None):
                return completions
        else:
            # do a final call without auto function calling
            return await self._send_chat_request(chat_history, settings)

    async def _send_chat_request(
        self, chat_history: ChatHistory, settings: GoogleAIChatPromptExecutionSettings
    ) -> list[ChatMessageContent]:
        """Send a chat request to the Google AI service."""
        genai.configure(api_key=self.service_settings.api_key.get_secret_value())
        model = GenerativeModel(
            self.service_settings.gemini_model_id,
            system_instruction=filter_system_message(chat_history),
        )

        response: AsyncGenerateContentResponse = await model.generate_content_async(
            contents=self._prepare_chat_history_for_request(chat_history),
            generation_config=GenerationConfig(**settings.prepare_settings_dict()),
            tools=settings.tools,
            tool_config=settings.tool_config,
        )

        return [self._create_chat_message_content(response, candidate) for candidate in response.candidates]

    def _create_chat_message_content(
        self, response: AsyncGenerateContentResponse, candidate: Candidate
    ) -> ChatMessageContent:
        """Create a chat message content object.

        Args:
            response: The response from the service.
            candidate: The candidate from the response.

        Returns:
            A chat message content object.
        """
        # Best effort conversion of finish reason. The raw value will be available in metadata.
        finish_reason: FinishReason | None = finish_reason_from_google_ai_to_semantic_kernel(candidate.finish_reason)
        response_metadata = self._get_metadata_from_response(response)
        response_metadata.update(self._get_metadata_from_candidate(candidate))

        items: list[ITEM_TYPES] = []
        for idx, part in enumerate(candidate.content.parts):
            if part.text:
                items.append(TextContent(text=part.text, inner_content=response, metadata=response_metadata))
            elif part.function_call:
                items.append(
                    FunctionCallContent(
                        id=f"{part.function_call.name}_{idx!s}",
                        name=format_gemini_function_name_to_kernel_function_fully_qualified_name(
                            part.function_call.name
                        ),
                        arguments={k: v for k, v in part.function_call.args.items()},
                    )
                )

        return ChatMessageContent(
            ai_model_id=self.ai_model_id,
            role=AuthorRole.ASSISTANT,
            items=items,
            inner_content=response,
            finish_reason=finish_reason,
            metadata=response_metadata,
        )

    # endregion

    # region Streaming
    @override
    async def get_streaming_chat_message_contents(
        self,
        chat_history: ChatHistory,
        settings: "PromptExecutionSettings",
        **kwargs: Any,
    ) -> AsyncGenerator[list[StreamingChatMessageContent], Any]:
        settings = self.get_prompt_execution_settings_from_settings(settings)
        assert isinstance(settings, GoogleAIChatPromptExecutionSettings)  # nosec

        async_generator = self._send_chat_streaming_request(chat_history, settings)

        async for messages in async_generator:
            yield messages

    async def _send_chat_streaming_request(
        self,
        chat_history: ChatHistory,
        settings: GoogleAIChatPromptExecutionSettings,
    ) -> AsyncGenerator[list[StreamingChatMessageContent], Any]:
        """Send a streaming chat request to the Google AI service."""
        genai.configure(api_key=self.service_settings.api_key.get_secret_value())
        model = GenerativeModel(
            self.service_settings.gemini_model_id,
            system_instruction=filter_system_message(chat_history),
        )

        response: AsyncGenerateContentResponse = await model.generate_content_async(
            contents=self._prepare_chat_history_for_request(chat_history),
            generation_config=GenerationConfig(**settings.prepare_settings_dict()),
            stream=True,
        )

        async for chunk in response:
            yield [self._create_streaming_chat_message_content(chunk, candidate) for candidate in chunk.candidates]

    def _create_streaming_chat_message_content(
        self,
        chunk: GenerateContentResponse,
        candidate: Candidate,
    ) -> StreamingChatMessageContent:
        """Create a streaming chat message content object.

        Args:
            chunk: The response from the service.
            candidate: The candidate from the response.

        Returns:
            A streaming chat message content object.
        """
        # Best effort conversion of finish reason. The raw value will be available in metadata.
        finish_reason: FinishReason | None = finish_reason_from_google_ai_to_semantic_kernel(candidate.finish_reason)
        response_metadata = self._get_metadata_from_response(chunk)
        response_metadata.update(self._get_metadata_from_candidate(candidate))

        return StreamingChatMessageContent(
            ai_model_id=self.ai_model_id,
            role=AuthorRole.ASSISTANT,
            choice_index=candidate.index,
            content=candidate.content.parts[0].text,
            inner_content=chunk,
            finish_reason=finish_reason,
            metadata=response_metadata,
        )

    # endregion

    @override
    def _prepare_chat_history_for_request(
        self,
        chat_history: ChatHistory,
        role_key: str = "role",
        content_key: str = "content",
    ) -> list[Content]:
        chat_request_messages: list[Content] = []

        for message in chat_history.messages:
            if message.role == AuthorRole.SYSTEM:
                # Skip system messages since they are not part of the chat request.
                # System message will be provided as system_instruction in the model.
                continue
            if message.role == AuthorRole.USER:
                chat_request_messages.append(Content(role="user", parts=format_user_message(message)))
            elif message.role == AuthorRole.ASSISTANT:
                chat_request_messages.append(Content(role="model", parts=format_assistant_message(message)))
            elif message.role == AuthorRole.TOOL:
                chat_request_messages.append(Content(role="function", parts=format_tool_message(message)))
            else:
                raise ValueError(f"Unsupported role: {message.role}")

        return chat_request_messages

    def _get_metadata_from_response(
        self, response: AsyncGenerateContentResponse | GenerateContentResponse
    ) -> dict[str, Any]:
        """Get metadata from the response.

        Args:
            response: The response from the service.

        Returns:
            A dictionary containing metadata.
        """
        return {
            "prompt_feedback": response.prompt_feedback,
            "usage": response.usage_metadata,
        }

    def _get_metadata_from_candidate(self, candidate: Candidate) -> dict[str, Any]:
        """Get metadata from the candidate.

        Args:
            candidate: The candidate from the response.

        Returns:
            A dictionary containing metadata.
        """
        return {
            "index": candidate.index,
            "finish_reason": candidate.finish_reason,
            "safety_ratings": candidate.safety_ratings,
            "token_count": candidate.token_count,
        }

    def _configure_function_choice_behavior(self, settings: GoogleAIChatPromptExecutionSettings, kernel: Kernel):
        """Configure the function choice behavior to include the kernel functions."""
        if not settings.function_choice_behavior:
            raise ServiceInvalidExecutionSettingsError("Function choice behavior is required for tool calls.")

        settings.function_choice_behavior.configure(
            kernel=kernel,
            update_settings_callback=update_settings_from_function_choice_configuration,
            settings=settings,
        )

    async def _invoke_function_calls(
        self,
        function_calls: list[FunctionCallContent],
        chat_history: ChatHistory,
        kernel: Kernel,
        arguments: KernelArguments | None,
        function_call_count: int,
        request_index: int,
        function_behavior: FunctionChoiceBehavior,
    ):
        """Invoke function calls."""
        logger.info(f"processing {function_call_count} tool calls in parallel.")

        return await asyncio.gather(
            *[
                kernel.invoke_function_call(
                    function_call=function_call,
                    chat_history=chat_history,
                    arguments=arguments,
                    function_call_count=function_call_count,
                    request_index=request_index,
                    function_behavior=function_behavior,
                )
                for function_call in function_calls
            ],
        )

    @override
    def get_prompt_execution_settings_class(
        self,
    ) -> type["PromptExecutionSettings"]:
        """Get the request settings class."""
        return GoogleAIChatPromptExecutionSettings
