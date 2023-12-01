# Copyright (c) Microsoft. All rights reserved.

from logging import Logger
from typing import Any, Dict, Mapping, Optional

from openai import AsyncOpenAI
from pydantic import Field, validate_call

from semantic_kernel.connectors.ai.open_ai.services.open_ai_handler import (
    OpenAIHandler,
)
from semantic_kernel.connectors.ai.open_ai.services.open_ai_model_types import (
    OpenAIModelTypes,
)


class OpenAIConfigBase(OpenAIHandler):
    @validate_call(config=dict(arbitrary_types_allowed=True))
    def __init__(
        self,
        ai_model_id: str = Field(min_length=1),
        api_key: str = Field(min_length=1),
        ai_model_type: Optional[OpenAIModelTypes] = OpenAIModelTypes.CHAT,
        org_id: Optional[str] = None,
        default_headers: Optional[Mapping[str, str]] = None,
        log: Optional[Logger] = None,
    ) -> None:
        """Initialize a client for OpenAI services.

        This constructor sets up a client to interact with OpenAI's API, allowing for different types of AI model interactions, like chat or text completion.

        Arguments:
            ai_model_id {str} -- OpenAI model identifier. Must be non-empty. Default to a preset value.
            api_key {str} -- OpenAI API key for authentication. Must be non-empty.
            ai_model_type {Optional[OpenAIModelTypes]} -- The type of OpenAI model to interact with. Defaults to CHAT.
            org_id {Optional[str]} -- OpenAI organization ID. This is optional unless the account belongs to multiple organizations.
            default_headers {Optional[Mapping[str, str]]} -- Default headers for HTTP requests. (Optional)
            log {Optional[Logger]} -- Logger instance for logging purposes. (Optional)

        The constructor also initializes an asynchronous OpenAI client with the provided API key, organization ID, and default headers.

        Note: The 'Field' function from Pydantic is used to enforce minimum length constraints on 'ai_model_id' and 'api_key'.
        """

        # TODO: add SK user-agent here
        client = AsyncOpenAI(
            api_key=api_key, 
            organization=org_id,
            default_headers=default_headers,
        )
        super().__init__(
            ai_model_id=ai_model_id,
            client=client,
            log=log,
            ai_model_type=ai_model_type,
        )

    def to_dict(self) -> Dict[str, str]:
        """
        Create a dict of the service settings.
        """
        client_settings = {
            "api_key": self.client.api_key,
            "default_headers": self.client.default_headers,
        }
        if self.client.organization:
            client_settings["org_id"] = self.client.organization
        base = self.model_dump(
            exclude={
                "prompt_tokens",
                "completion_tokens",
                "total_tokens",
                "api_type",
                "ai_model_type",
                "client",
            },
            by_alias=True,
            exclude_none=True,
        )
        base.update(client_settings)
        return base

    def get_model_args(self) -> Dict[str, Any]:
        return {
            "model": self.ai_model_id,
        }
