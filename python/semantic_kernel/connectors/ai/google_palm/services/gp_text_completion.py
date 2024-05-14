# Copyright (c) Microsoft. All rights reserved.

import logging
from typing import Annotated, List

import google.generativeai as palm
from google.generativeai.types import Completion
from google.generativeai.types.text_types import TextCompletion
from pydantic import StringConstraints, ValidationError

from semantic_kernel.connectors.ai.google_palm.gp_prompt_execution_settings import GooglePalmTextPromptExecutionSettings
from semantic_kernel.connectors.ai.google_palm.settings.google_palm_settings import GooglePalmSettings
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.connectors.ai.text_completion_client_base import TextCompletionClientBase
from semantic_kernel.contents.text_content import TextContent
from semantic_kernel.exceptions import ServiceResponseException

logger: logging.Logger = logging.getLogger(__name__)


class GooglePalmTextCompletion(TextCompletionClientBase):
    api_key: Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]

    def __init__(self, ai_model_id: str, api_key: str | None = None, env_file_path: str | None = None):
        """
        Initializes a new instance of the GooglePalmTextCompletion class.

        Arguments:
            ai_model_id {str} -- GooglePalm model name, see
                https://developers.generativeai.google/models/language
            api_key {str | None} -- The optional API key to use. If not provided, will be
                read from either the env vars or the .env settings file.
            env_file_path {str | None} -- Use the environment settings file as a
                fallback to environment variables. (Optional)
        """
        try:
            google_palm_settings = GooglePalmSettings.create(env_file_path=env_file_path)
        except ValidationError as e:
            logger.warning(f"Error loading Google Palm pydantic settings: {e}")

        api_key = api_key or (
            google_palm_settings.api_key.get_secret_value()
            if google_palm_settings and google_palm_settings.api_key
            else None
        )
        ai_model_id = ai_model_id or (
            google_palm_settings.text_model_id if google_palm_settings and google_palm_settings.text_model_id else None
        )

        super().__init__(ai_model_id=ai_model_id, api_key=api_key)

    async def complete(self, prompt: str, settings: GooglePalmTextPromptExecutionSettings) -> List[TextContent]:
        """
        This is the method that is called from the kernel to get a response from a text-optimized LLM.

        Arguments:
            prompt {str} -- The prompt to send to the LLM.
            settings {GooglePalmTextPromptExecutionSettings} -- Settings for the request.

        Returns:
            List[TextContent] -- A list of TextContent objects representing the response(s) from the LLM.
        """
        settings.prompt = prompt
        if not settings.ai_model_id:
            settings.ai_model_id = self.ai_model_id
        try:
            palm.configure(api_key=self.api_key)
        except Exception as ex:
            raise PermissionError(
                "Google PaLM service failed to configure. Invalid API key provided.",
                ex,
            )
        try:
            response = palm.generate_text(**settings.prepare_settings_dict())
        except Exception as ex:
            raise ServiceResponseException(
                "Google PaLM service failed to complete the prompt",
                ex,
            ) from ex
        return [self._create_text_content(response, candidate) for candidate in response.candidates]

    def _create_text_content(self, response: Completion, candidate: TextCompletion) -> TextContent:
        """Create a text content object from a candidate."""
        return TextContent(
            inner_content=response,
            ai_model_id=self.ai_model_id,
            text=candidate.get("output"),
            metadata={
                "filters": response.filters,
                "safety_feedback": response.safety_feedback,
                "citation_metadata": candidate.get("citation_metadata"),
                "safety_ratings": candidate.get("safety_ratings"),
            },
        )

    async def complete_stream(
        self,
        prompt: str,
        settings: GooglePalmTextPromptExecutionSettings,
    ):
        raise NotImplementedError("Google Palm API does not currently support streaming")

    def get_prompt_execution_settings_class(self) -> "PromptExecutionSettings":
        """Create a request settings object."""
        return GooglePalmTextPromptExecutionSettings
