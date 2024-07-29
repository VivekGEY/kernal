# Copyright (c) Microsoft. All rights reserved.

import logging
from collections.abc import AsyncIterable, Awaitable, Callable
from copy import copy
from typing import TYPE_CHECKING, Any

from openai import AsyncAzureOpenAI
from pydantic import ValidationError

from semantic_kernel.agents.open_ai.open_ai_assistant_base import OpenAIAssistantBase
from semantic_kernel.connectors.ai.open_ai.settings.azure_open_ai_settings import AzureOpenAISettings
from semantic_kernel.const import DEFAULT_SERVICE_NAME
from semantic_kernel.exceptions.agent_exceptions import AgentInitializationError
from semantic_kernel.kernel_pydantic import HttpsUrl
from semantic_kernel.utils.experimental_decorator import experimental_class
from semantic_kernel.utils.telemetry.user_agent import APP_INFO, prepend_semantic_kernel_to_user_agent

if TYPE_CHECKING:
    from semantic_kernel.kernel import Kernel


logger: logging.Logger = logging.getLogger(__name__)


@experimental_class
class AzureOpenAIAssistantAgent(OpenAIAssistantBase):
    """Azure OpenAI Assistant Agent class.

    Provides the ability to interact with Azure OpenAI Assistants.
    """

    # region Agent Initialization

    def __init__(
        self,
        kernel: "Kernel | None" = None,
        service_id: str | None = None,
        deployment_name: str | None = None,
        api_key: str | None = None,
        endpoint: str | None = None,
        api_version: str | None = None,
        ad_token: str | None = None,
        ad_token_provider: Callable[[], str | Awaitable[str]] | None = None,
        client: AsyncAzureOpenAI | None = None,
        default_headers: dict[str, str] | None = None,
        env_file_path: str | None = None,
        env_file_encoding: str | None = None,
        description: str | None = None,
        id: str | None = None,
        instructions: str | None = None,
        name: str | None = None,
        enable_code_interpreter: bool | None = None,
        enable_file_search: bool | None = None,
        enable_json_response: bool | None = None,
        file_ids: list[str] | None = [],
        temperature: float | None = None,
        top_p: float | None = None,
        vector_store_id: str | None = None,
        metadata: dict[str, Any] | None = {},
        max_completion_tokens: int | None = None,
        max_prompt_tokens: int | None = None,
        parallel_tool_calls_enabled: bool | None = True,
        truncation_message_count: int | None = None,
        **kwargs: Any,
    ) -> None:
        """Initialize an Azure OpenAI Assistant Agent.

        Args:
            kernel: The Kernel instance. (optional)
            service_id: The service ID. (optional)
            deployment_name: The deployment name. (optional)
            api_key: The Azure OpenAI API key. (optional)
            endpoint: The Azure OpenAI endpoint. (optional)
            api_version: The Azure OpenAI API version. (optional)
            ad_token: The Azure AD token. (optional)
            ad_token_provider: The Azure AD token provider. (optional)
            client: The Azure OpenAI client. (optional)
            default_headers: The default headers. (optional)
            env_file_path: The environment file path. (optional)
            env_file_encoding: The environment file encoding. (optional)
            description: The description. (optional)
            id: The Agent ID. (optional)
            instructions: The Agent instructions. (optional)
            name: The Agent name. (optional)
            enable_code_interpreter: Enable the code interpreter. (optional)
            enable_file_search: Enable the file search. (optional)
            enable_json_response: Enable the JSON response. (optional)
            file_ids: The file IDs. (optional)
            temperature: The temperature. (optional)
            top_p: The top p. (optional)
            vector_store_id: The vector store ID. (optional)
            metadata: The metadata. (optional)
            max_completion_tokens: The maximum completion tokens. (optional)
            max_prompt_tokens: The maximum prompt tokens. (optional)
            parallel_tool_calls_enabled: Enable parallel tool calls. (optional)
            truncation_message_count: The truncation message count. (optional)
            **kwargs: Additional keyword arguments.

        Raises:
            AgentInitializationError: If the api_key is not provided in the configuration.
        """
        try:
            azure_openai_settings = AzureOpenAISettings.create(
                api_key=api_key,
                endpoint=endpoint,
                chat_deployment_name=deployment_name,
                api_version=api_version,
                env_file_path=env_file_path,
                env_file_encoding=env_file_encoding,
            )
        except ValidationError as ex:
            raise AgentInitializationError("Failed to create Azure OpenAI settings.", ex) from ex

        if not azure_openai_settings.chat_deployment_name:
            raise AgentInitializationError("The Azure OpenAI chat_deployment_name is required.")

        if not azure_openai_settings.api_key and not ad_token and not ad_token_provider:
            raise AgentInitializationError("Please provide either api_key, ad_token or ad_token_provider.")
        client = self._create_client(
            api_key=azure_openai_settings.api_key.get_secret_value() if azure_openai_settings.api_key else None,
            endpoint=azure_openai_settings.endpoint,
            api_version=azure_openai_settings.api_version,
            ad_token=ad_token,
            ad_token_provider=ad_token_provider,
            default_headers=default_headers,
        )
        service_id = service_id if service_id else DEFAULT_SERVICE_NAME

        args: dict[str, Any] = {
            "kernel": kernel,
            "ai_model_id": azure_openai_settings.chat_deployment_name,
            "service_id": service_id,
            "client": client,
            "name": name,
            "description": description,
            "instructions": instructions,
            "enable_code_interpreter": enable_code_interpreter,
            "enable_file_search": enable_file_search,
            "enable_json_response": enable_json_response,
            "file_ids": file_ids,
            "temperature": temperature,
            "top_p": top_p,
            "vector_store_id": vector_store_id,
            "metadata": metadata,
            "max_completion_tokens": max_completion_tokens,
            "max_prompt_tokens": max_prompt_tokens,
            "parallel_tool_calls_enabled": parallel_tool_calls_enabled,
            "truncation_message_count": truncation_message_count,
        }

        if id is not None:
            args["id"] = id
        if kernel is not None:
            args["kernel"] = kernel
        if kwargs:
            args.update(kwargs)
        super().__init__(**args)

    @classmethod
    async def create(
        cls,
        *,
        kernel: "Kernel | None" = None,
        service_id: str | None = None,
        deployment_name: str | None = None,
        api_key: str | None = None,
        endpoint: str | None = None,
        api_version: str | None = None,
        ad_token: str | None = None,
        ad_token_provider: Callable[[], str | Awaitable[str]] | None = None,
        client: AsyncAzureOpenAI | None = None,
        default_headers: dict[str, str] | None = None,
        env_file_path: str | None = None,
        env_file_encoding: str | None = None,
        description: str | None = None,
        id: str | None = None,
        instructions: str | None = None,
        name: str | None = None,
        enable_code_interpreter: bool | None = None,
        enable_file_search: bool | None = None,
        enable_json_response: bool | None = None,
        file_ids: list[str] | None = [],
        temperature: float | None = None,
        top_p: float | None = None,
        vector_store_id: str | None = None,
        metadata: dict[str, Any] | None = {},
        max_completion_tokens: int | None = None,
        max_prompt_tokens: int | None = None,
        parallel_tool_calls_enabled: bool | None = True,
        truncation_message_count: int | None = None,
        **kwargs: Any,
    ) -> "AzureOpenAIAssistantAgent":
        """Asynchronous class method used to create the OpenAI Assistant Agent.

        Args:
            kernel: The Kernel instance. (optional)
            service_id: The service ID. (optional)
            deployment_name: The deployment name. (optional)
            api_key: The Azure OpenAI API key. (optional)
            endpoint: The Azure OpenAI endpoint. (optional)
            api_version: The Azure OpenAI API version. (optional)
            ad_token: The Azure AD token. (optional)
            ad_token_provider: The Azure AD token provider. (optional)
            client: The Azure OpenAI client. (optional)
            default_headers: The default headers. (optional)
            env_file_path: The environment file path. (optional)
            env_file_encoding: The environment file encoding. (optional)
            description: The description. (optional)
            id: The Agent ID. (optional)
            instructions: The Agent instructions. (optional)
            name: The Agent name. (optional)
            enable_code_interpreter: Enable the code interpreter. (optional)
            enable_file_search: Enable the file search. (optional)
            enable_json_response: Enable the JSON response. (optional)
            file_ids: The file IDs. (optional)
            temperature: The temperature. (optional)
            top_p: The top p. (optional)
            vector_store_id: The vector store ID. (optional)
            metadata: The metadata. (optional)
            max_completion_tokens: The maximum completion tokens. (optional)
            max_prompt_tokens: The maximum prompt tokens. (optional)
            parallel_tool_calls_enabled: Enable parallel tool calls. (optional)
            truncation_message_count: The truncation message count. (optional)
            **kwargs: Additional keyword arguments.

        Returns:
            An instance of the AzureOpenAIAssistantAgent
        """
        agent = cls(
            kernel=kernel,
            service_id=service_id,
            deployment_name=deployment_name,
            api_key=api_key,
            endpoint=endpoint,
            api_version=api_version,
            ad_token=ad_token,
            ad_token_provider=ad_token_provider,
            client=client,
            default_headers=default_headers,
            env_file_path=env_file_path,
            env_file_encoding=env_file_encoding,
            description=description,
            id=id,
            instructions=instructions,
            name=name,
            enable_code_interpreter=enable_code_interpreter,
            enable_file_search=enable_file_search,
            enable_json_response=enable_json_response,
            file_ids=file_ids,
            temperature=temperature,
            top_p=top_p,
            vector_store_id=vector_store_id,
            metadata=metadata,
            max_completion_tokens=max_completion_tokens,
            max_prompt_tokens=max_prompt_tokens,
            parallel_tool_calls_enabled=parallel_tool_calls_enabled,
            truncation_message_count=truncation_message_count,
            **kwargs,
        )
        agent.assistant = await agent.create_assistant()
        return agent

    @staticmethod
    def _create_client(
        api_key: str | None = None,
        endpoint: HttpsUrl | None = None,
        api_version: str | None = None,
        ad_token: str | None = None,
        ad_token_provider: Callable[[], str | Awaitable[str]] | None = None,
        default_headers: dict[str, str] | None = None,
    ) -> AsyncAzureOpenAI:
        """Create the OpenAI client from configuration.

        Args:
            api_key: The OpenAI API key.
            endpoint: The OpenAI endpoint.
            api_version: The OpenAI API version.
            ad_token: The Azure AD token.
            ad_token_provider: The Azure AD token provider.
            default_headers: The default headers.

        Returns:
            An AsyncAzureOpenAI client instance.
        """
        merged_headers = dict(copy(default_headers)) if default_headers else {}
        if APP_INFO:
            merged_headers.update(APP_INFO)
            merged_headers = prepend_semantic_kernel_to_user_agent(merged_headers)

        if not api_key and not ad_token and not ad_token_provider:
            raise AgentInitializationError(
                "Please provide either AzureOpenAI api_key, an ad_token or an ad_token_provider or a client."
            )
        if not endpoint:
            raise AgentInitializationError("Please provide an AzureOpenAI endpoint.")
        return AsyncAzureOpenAI(
            azure_endpoint=str(endpoint),
            api_version=api_version,
            api_key=api_key,
            azure_ad_token=ad_token,
            azure_ad_token_provider=ad_token_provider,
            default_headers=merged_headers,
        )

    async def list_definitions(self) -> AsyncIterable[dict[str, Any]]:
        """List the assistant definitions.

        Yields:
            An AsyncIterable of dictionaries representing the OpenAIAssistantDefinition.
        """
        assistants = await self.client.beta.assistants.list(order="desc")
        for assistant in assistants.data:
            yield self._create_open_ai_assistant_definition(assistant)

    async def retrieve(
        self,
        id: str,
        api_key: str | None = None,
        endpoint: HttpsUrl | None = None,
        api_version: str | None = None,
        ad_token: str | None = None,
        ad_token_provider: Callable[[], str | Awaitable[str]] | None = None,
        client: AsyncAzureOpenAI | None = None,
        kernel: "Kernel | None" = None,
        default_headers: dict[str, str] | None = None,
    ) -> "AzureOpenAIAssistantAgent":
        """Retrieve an assistant by ID.

        Args:
            id: The assistant ID.
            api_key: The Azure OpenAI API
            endpoint: The Azure OpenAI endpoint. (optional)
            api_version: The Azure OpenAI API version. (optional)
            ad_token: The Azure AD token. (optional)
            ad_token_provider: The Azure AD token provider. (optional)
            client: The Azure OpenAI client. (optional)
            kernel: The Kernel instance. (optional)
            default_headers: The default headers. (optional)

        Returns:
            An OpenAIAssistantAgent instance.
        """
        client = self._create_client(
            api_key=api_key,
            endpoint=endpoint,
            api_version=api_version,
            ad_token=ad_token,
            ad_token_provider=ad_token_provider,
            default_headers=default_headers,
        )
        assistant = await client.beta.assistants.retrieve(id)
        assistant_definition = self._create_open_ai_assistant_definition(assistant)
        return AzureOpenAIAssistantAgent(kernel=kernel, **assistant_definition)

    # endregion
