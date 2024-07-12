# Copyright (c) Microsoft. All rights reserved.

from unittest.mock import patch

import pytest
from numpy import array

from semantic_kernel.connectors.ai.ollama.ollama_prompt_execution_settings import OllamaEmbeddingPromptExecutionSettings
from semantic_kernel.connectors.ai.ollama.services.ollama_text_embedding import OllamaTextEmbedding
from semantic_kernel.exceptions.service_exceptions import ServiceInitializationError


def test_init_empty_service_id(model_id):
    """Test that the service initializes correctly with an empty service id."""
    ollama = OllamaTextEmbedding(ai_model_id=model_id)
    assert ollama.service_id == model_id


@pytest.mark.parametrize("exclude_list", [["OLLAMA_MODEL"]], indirect=True)
def test_init_empty_model_id(ollama_unit_test_env):
    """Test that the service initializes incorrectly with an empty model id."""
    with pytest.raises(ServiceInitializationError):
        _ = OllamaTextEmbedding(env_file_path="fake_env_file_path.env")


@pytest.mark.asyncio
@patch("ollama.AsyncClient.__init__", return_value=None)  # mock_client
@patch("ollama.AsyncClient.embeddings")  # mock_embedding_client
async def test_custom_host(mock_embedding_client, mock_client, model_id, host, prompt):
    """Test that the service initializes and generates embeddings correctly with a custom host."""
    mock_embedding_client.return_value = {"embedding": [0.1, 0.2, 0.3]}

    ollama = OllamaTextEmbedding(ai_model_id=model_id, host=host)
    _ = await ollama.generate_embeddings(
        [prompt],
    )

    mock_client.assert_called_once_with(host=host)


@pytest.mark.asyncio
@patch("ollama.AsyncClient.embeddings")
async def test_embedding(mock_embedding_client, model_id, prompt):
    """Test that the service initializes and generates embeddings correctly."""
    mock_embedding_client.return_value = {"embedding": [0.1, 0.2, 0.3]}
    settings = OllamaEmbeddingPromptExecutionSettings()
    settings.options = {"test_key": "test_value"}

    ollama = OllamaTextEmbedding(ai_model_id=model_id)
    response = await ollama.generate_embeddings(
        [prompt],
        settings=settings,
    )
    assert response.all() == array([0.1, 0.2, 0.3]).all()
    mock_embedding_client.assert_called_once_with(model=model_id, prompt=prompt, options=settings.options)
