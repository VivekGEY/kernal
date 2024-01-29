# Copyright (c) Microsoft. All rights reserved.

import logging
from typing import TYPE_CHECKING, AsyncGenerator, List, Union

from openai.types.completion import Completion

from semantic_kernel.connectors.ai import TextCompletionClientBase
from semantic_kernel.connectors.ai.open_ai.prompt_execution_settings.open_ai_prompt_execution_settings import (
    OpenAITextPromptExecutionSettings,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_handler import (
    OpenAIHandler,
)
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings

if TYPE_CHECKING:
    from semantic_kernel.connectors.ai.open_ai.prompt_execution_settings.open_ai_prompt_execution_settings import (
        OpenAIPromptExecutionSettings,
    )

logger: logging.Logger = logging.getLogger(__name__)


class OpenAITextCompletionBase(TextCompletionClientBase, OpenAIHandler):
    async def complete(
        self,
        prompt: str,
        settings: "OpenAIPromptExecutionSettings",
        **kwargs,
    ) -> Union[str, List[str]]:
        """Executes a completion request and returns the result.

        Arguments:
            prompt {str} -- The prompt to use for the completion request.
            settings {OpenAIPromptExecutionSettings} -- The settings to use for the completion request.

        Returns:
            Union[str, List[str]] -- The completion result(s).
        """
        if isinstance(settings, OpenAITextPromptExecutionSettings):
            settings.prompt = prompt
        else:
            settings.messages = [{"role": "user", "content": prompt}]
        if settings.ai_model_id is None:
            settings.ai_model_id = self.ai_model_id
        response = await self._send_request(prompt_execution_settings=settings)

        if isinstance(response, Completion):
            if len(response.choices) == 1:
                return response.choices[0].text
            return [choice.text for choice in response.choices]
        if len(response.choices) == 1:
            return response.choices[0].message.content
        return [choice.message.content for choice in response.choices]

    async def complete_stream(
        self,
        prompt: str,
        settings: "OpenAIPromptExecutionSettings",
        **kwargs,
    ) -> AsyncGenerator[Union[str, List[str]], None]:
        """
        Executes a completion request and streams the result.
        Supports both chat completion and text completion.

        Arguments:
            prompt {str} -- The prompt to use for the completion request.
            settings {OpenAIPromptExecutionSettings} -- The settings to use for the completion request.

        Returns:
            Union[str, List[str]] -- The completion result(s).
        """
        if "prompt" in settings.model_fields:
            settings.prompt = prompt
        if "messages" in settings.model_fields:
            if not settings.messages:
                settings.messages = [{"role": "user", "content": prompt}]
            else:
                settings.messages.append({"role": "user", "content": prompt})
        settings.ai_model_id = self.ai_model_id
        settings.stream = True
        response = await self._send_request(prompt_execution_settings=settings)

        async for partial in response:
            if len(partial.choices) == 0:
                continue

            if settings.number_of_responses > 1:
                completions = [""] * settings.number_of_responses
                for choice in partial.choices:
                    if hasattr(choice, "delta") and hasattr(choice.delta, "content"):  # Chat completion
                        completions[choice.index] = choice.delta.content
                    elif hasattr(choice, "text"):  # Text completion
                        completions[choice.index] = choice.text
                if any(completions):
                    yield completions
            else:
                if hasattr(partial.choices[0], "delta") and hasattr(
                    partial.choices[0].delta, "content"
                ):  # Chat completion
                    content = partial.choices[0].delta.content
                    if content:
                        yield content
                elif hasattr(partial.choices[0], "text"):  # Text completion
                    text = partial.choices[0].text
                    if text.strip():  # Exclude empty or whitespace-only text
                        yield text

    def get_prompt_execution_settings_class(self) -> "PromptExecutionSettings":
        """Create a request settings object."""
        return OpenAITextPromptExecutionSettings
