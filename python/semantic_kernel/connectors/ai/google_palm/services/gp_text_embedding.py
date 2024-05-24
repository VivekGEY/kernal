# Copyright (c) Microsoft. All rights reserved.

import logging
import sys
from typing import Annotated, Any

if sys.version_info >= (3, 12):
    from typing import overload
else:
    from typing_extensions import overload

import google.generativeai as palm
from numpy import array, ndarray
from pydantic import StringConstraints, ValidationError

from semantic_kernel.connectors.ai.embeddings.embedding_generator_base import EmbeddingGeneratorBase
from semantic_kernel.connectors.ai.google_palm.settings.google_palm_settings import GooglePalmSettings
from semantic_kernel.exceptions import ServiceInvalidAuthError, ServiceResponseException
from semantic_kernel.utils.experimental_decorator import experimental_class

logger: logging.Logger = logging.getLogger(__name__)


@experimental_class
class GooglePalmTextEmbedding(EmbeddingGeneratorBase):
    api_key: Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]

    def __init__(self, ai_model_id: str, api_key: str | None = None, env_file_path: str | None = None) -> None:
        """Initializes a new instance of the GooglePalmTextEmbedding class.

        Args:
            ai_model_id (str): GooglePalm model name, see
                https://developers.generativeai.google/models/language
            api_key (str | None): The optional API key to use. If not provided, will be
                read from either the env vars or the .env settings file.
            env_file_path (str | None): Use the environment settings file
                as a fallback to environment variables. (Optional)
        """
        try:
            google_palm_settings = GooglePalmSettings.create(env_file_path=env_file_path)
        except ValidationError as e:
            logger.error(f"Error loading Google Palm pydantic settings: {e}")

        api_key = api_key or (
            google_palm_settings.api_key.get_secret_value()
            if google_palm_settings and google_palm_settings.api_key
            else None
        )
        ai_model_id = ai_model_id or (
            google_palm_settings.embedding_model_id
            if google_palm_settings and google_palm_settings.embedding_model_id
            else None
        )
        super().__init__(ai_model_id=ai_model_id, api_key=api_key)

    @overload
    async def generate_embeddings(self, texts: list[str], **kwargs: Any) -> ndarray:
        try:
            palm.configure(api_key=self.api_key)
        except Exception as ex:
            raise ServiceInvalidAuthError(
                "Google PaLM service failed to configure. Invalid API key provided.",
                ex,
            ) from ex
        embeddings = []
        for text in texts:
            try:
                response = palm.generate_embeddings(model=self.ai_model_id, text=text, **kwargs)
                embeddings.append(array(response["embedding"]))
            except Exception as ex:
                raise ServiceResponseException(
                    "Google PaLM service failed to generate the embedding.",
                    ex,
                ) from ex
        return array(embeddings)
