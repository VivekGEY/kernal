# Copyright (c) Microsoft. All rights reserved.

import os
import sys
from functools import partial
from typing import Any

import pytest

from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.contents import ChatHistory, ChatMessageContent, TextContent
from semantic_kernel.contents.image_content import ImageContent
from semantic_kernel.contents.utils.author_role import AuthorRole
from tests.integration.completions.test_chat_completion_base import TestChatCompletionBase
from tests.integration.completions.test_completion_base import ServiceType
from tests.integration.completions.test_utils import retry

if sys.version_info >= (3, 12):
    from typing import override  # pragma: no cover
else:
    from typing_extensions import override  # pragma: no cover


pytestmark = pytest.mark.parametrize(
    "service_id, execution_settings_kwargs, inputs",
    [
        pytest.param(
            "openai",
            {},
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent(
                            uri="https://upload.wikimedia.org/wikipedia/commons/d/d5/Half-timbered_mansion%2C_Zirkel%2C_East_view.jpg"
                        ),
                    ],
                ),
                ChatMessageContent(role=AuthorRole.USER, items=[TextContent(text="Where was it made?")]),
            ],
            id="openai_image_input_uri",
        ),
        pytest.param(
            "openai",
            {},
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent.from_image_path(
                            image_path=os.path.join(os.path.dirname(__file__), "../../", "assets/sample_image.jpg")
                        ),
                    ],
                ),
                ChatMessageContent(role=AuthorRole.USER, items=[TextContent(text="Where was it made?")]),
            ],
            id="openai_image_input_file",
        ),
        pytest.param(
            "azure",
            {},
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent(
                            uri="https://upload.wikimedia.org/wikipedia/commons/d/d5/Half-timbered_mansion%2C_Zirkel%2C_East_view.jpg"
                        ),
                    ],
                ),
                ChatMessageContent(role=AuthorRole.USER, items=[TextContent(text="Where was it made?")]),
            ],
            id="azure_image_input_uri",
        ),
        pytest.param(
            "azure",
            {},
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent.from_image_path(
                            image_path=os.path.join(os.path.dirname(__file__), "../../", "assets/sample_image.jpg")
                        ),
                    ],
                ),
                ChatMessageContent(role=AuthorRole.USER, items=[TextContent(text="Where was it made?")]),
            ],
            id="azure_image_input_file",
        ),
        pytest.param(
            "azure_ai_inference",
            {
                "max_tokens": 256,
            },
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent(
                            uri="https://upload.wikimedia.org/wikipedia/commons/d/d5/Half-timbered_mansion%2C_Zirkel%2C_East_view.jpg"
                        ),
                    ],
                ),
                ChatMessageContent(role=AuthorRole.USER, items=[TextContent(text="Where was it made?")]),
            ],
            id="azure_ai_inference_image_input_uri",
        ),
        pytest.param(
            "azure_ai_inference",
            {
                "max_tokens": 256,
            },
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent.from_image_path(
                            image_path=os.path.join(os.path.dirname(__file__), "../../", "assets/sample_image.jpg")
                        ),
                    ],
                ),
                ChatMessageContent(role=AuthorRole.USER, items=[TextContent(text="Where was it made?")]),
            ],
            id="azure_ai_inference_image_input_file",
        ),
        pytest.param(
            "google_ai",
            {
                "max_tokens": 256,
            },
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent.from_image_path(
                            image_path=os.path.join(os.path.dirname(__file__), "../../", "assets/sample_image.jpg")
                        ),
                    ],
                ),
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[TextContent(text="Where was it made? Make a guess if you are not sure.")],
                ),
            ],
            id="google_ai_image_input_file",
        ),
        pytest.param(
            "vertex_ai",
            {
                "max_tokens": 256,
            },
            [
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[
                        TextContent(text="What is in this image?"),
                        ImageContent.from_image_path(
                            image_path=os.path.join(os.path.dirname(__file__), "../../", "assets/sample_image.jpg")
                        ),
                    ],
                ),
                ChatMessageContent(
                    role=AuthorRole.USER,
                    items=[TextContent(text="Where was it made? Make a guess if you are not sure.")],
                ),
            ],
            id="vertex_ai_image_input_file",
        ),
    ],
)


class TestChatCompletionWithImageInput(TestChatCompletionBase):
    """Test chat completion with image input."""

    @override
    @pytest.mark.asyncio(scope="module")
    async def test_completion(
        self,
        kernel: Kernel,
        service_id: str,
        services: dict[str, tuple[ServiceType, type[PromptExecutionSettings]]],
        execution_settings_kwargs: dict[str, Any],
        inputs: list[str | ChatMessageContent | list[ChatMessageContent]],
    ):
        self.setup(kernel, service_id, services, execution_settings_kwargs)

        history = ChatHistory()
        for message in inputs:
            if isinstance(message, list):
                for msg in message:
                    history.add_message(msg)
            else:
                history.add_message(message)

            cmc = await retry(partial(self.execute_invoke, kernel=kernel, input=history, stream=False), retries=5)
            history.add_message(cmc)

        self.evaluate_response(history, inputs=inputs)

    @override
    @pytest.mark.asyncio(scope="module")
    async def test_streaming_chat_completion(
        self,
        kernel: Kernel,
        service_id: str,
        services: dict[str, tuple[ServiceType, type[PromptExecutionSettings]]],
        execution_settings_kwargs: dict[str, Any],
        inputs: list[str | ChatMessageContent | list[ChatMessageContent]],
    ):
        self.setup(kernel, service_id, services, execution_settings_kwargs)

        history = ChatHistory()
        for message in inputs:
            if isinstance(message, list):
                for msg in message:
                    history.add_message(msg)
            else:
                history.add_message(message)
            cmc = await retry(partial(self.execute_invoke, kernel=kernel, input=history, stream=True), retries=5)
            history.add_message(cmc)

        self.evaluate_response(history, inputs=inputs)

    @override
    def evaluate_response(self, response: Any, **kwargs):
        inputs = kwargs.get("inputs")
        assert len(response) == len(inputs) * 2
        for i in range(len(inputs)):
            message = response[i * 2 + 1]
            assert message.items, "No items in message"
            assert len(message.items) == 1, "Unexpected number of items in message"
            assert isinstance(message.items[0], TextContent), "Unexpected message item type"
            assert message.items[0].text, "Empty message text"
