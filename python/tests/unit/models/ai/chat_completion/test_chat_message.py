import pytest

from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.models.ai.chat_completion.chat_message import ChatMessage
from semantic_kernel.prompt_template.prompt_template import PromptTemplate
from semantic_kernel.prompt_template.prompt_template_config import (
    PromptTemplateConfig,
)


def test_chat_message():
    # Test initialization with default values
    message = ChatMessage()
    assert message.role == "assistant"
    assert message.fixed_content is None
    assert message.content is None
    assert message.content_template is None


@pytest.mark.asyncio
async def test_chat_message_rendering(create_kernel):
    # Test initialization with custom values
    kernel = create_kernel
    expected_content = "Hello, world!"
    prompt_config = PromptTemplateConfig.from_execution_settings(max_tokens=2000, temperature=0.7, top_p=0.8)
    content_template = PromptTemplate("Hello, {{$input}}!", kernel.prompt_template_engine, prompt_config)

    message = ChatMessage(
        role="user",
        content_template=content_template,
    )
    arguments = KernelArguments(input="world")
    await message.render_message(kernel, arguments)
    assert message.role == "user"
    assert message.fixed_content == expected_content
    assert message.content_template == content_template

    # Test content property
    assert message.content == expected_content
