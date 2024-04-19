# Copyright (c) Microsoft. All rights reserved.

from typing import List
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from openai import AsyncOpenAI

from semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion import OpenAIChatCompletionBase
from semantic_kernel.connectors.ai.open_ai.services.tool_call_behavior import ToolCallBehavior
from semantic_kernel.contents.chat_history import ChatHistory
from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.contents.function_call_content import FunctionCallContent
from semantic_kernel.contents.streaming_chat_message_content import StreamingChatMessageContent
from semantic_kernel.exceptions import FunctionCallInvalidArgumentsException
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.kernel import Kernel


async def mock_async_process_chat_stream_response(arg1, response, tool_call_behavior, chat_history, kernel, arguments):
    mock_content = MagicMock(spec=StreamingChatMessageContent)
    yield [mock_content], None


@pytest.mark.asyncio
async def test_complete_chat_stream(kernel: Kernel):
    chat_history = MagicMock()
    settings = MagicMock()
    mock_response = MagicMock()
    arguments = KernelArguments()

    with patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._get_tool_call_behavior",
        return_value=ToolCallBehavior(auto_invoke_kernel_functions=True, max_auto_invoke_attempts=3),
    ) as settings_mock, patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._prepare_settings",
        return_value=settings,
    ) as prepare_settings_mock, patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._send_chat_stream_request",
        return_value=mock_response,
    ) as mock_send_chat_stream_request, patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._process_chat_stream_response",
        new_callable=lambda: mock_async_process_chat_stream_response,
    ):
        chat_completion_base = OpenAIChatCompletionBase(
            ai_model_id="test_model_id", service_id="test", client=MagicMock(spec=AsyncOpenAI)
        )

        async for content in chat_completion_base.complete_chat_stream(
            chat_history, settings, kernel=kernel, arguments=arguments
        ):
            assert content is not None

        settings_mock.assert_called_once_with(settings)
        prepare_settings_mock.assert_called_with(settings, chat_history, stream_request=True)
        mock_send_chat_stream_request.assert_called_with(settings)


@pytest.mark.parametrize("tool_call", [False, True])
@pytest.mark.asyncio
async def test_complete_chat(tool_call, kernel: Kernel):
    chat_history = MagicMock()
    settings = MagicMock()
    mock_message_content = MagicMock(spec=List[ChatMessageContent])
    arguments = KernelArguments()

    with patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._get_tool_call_behavior",
        return_value=ToolCallBehavior(auto_invoke_kernel_functions=True, max_auto_invoke_attempts=3),
    ) as settings_mock, patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._prepare_settings",
        return_value=settings,
    ) as prepare_settings_mock, patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._send_chat_request",
        return_value=mock_message_content,
    ) as mock_send_chat_request, patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._should_return_completions_response",
        return_value=not tool_call,
    ), patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.OpenAIChatCompletionBase._process_chat_response_with_tool_call",
    ) as mock_process_chat_response_with_tool_call:
        chat_completion_base = OpenAIChatCompletionBase(
            ai_model_id="test_model_id", service_id="test", client=MagicMock(spec=AsyncOpenAI)
        )

        result = await chat_completion_base.complete_chat(chat_history, settings, kernel=kernel, arguments=arguments)

        if tool_call:
            assert result is None
        else:
            assert result is not None

        settings_mock.assert_called_once_with(settings)
        prepare_settings_mock.assert_called_with(settings, chat_history, stream_request=False)
        mock_send_chat_request.assert_called_with(settings)
        if tool_call:
            mock_process_chat_response_with_tool_call.assert_called()


@pytest.mark.asyncio
async def test_process_tool_calls():
    tool_call_mock = MagicMock(spec=FunctionCallContent)
    tool_call_mock.split_name_dict.return_value = {"arg_name": "arg_value"}
    tool_call_mock.to_kernel_arguments.return_value = {"arg_name": "arg_value"}
    tool_call_mock.name = "test_function"
    tool_call_mock.arguments = {"arg_name": "arg_value"}
    tool_call_mock.ai_model_id = None
    tool_call_mock.metadata = {}
    tool_call_mock.parse_arguments.return_value = {"arg_name": "arg_value"}
    tool_call_mock.id = "test_id"
    result_mock = MagicMock(spec=ChatMessageContent)
    result_mock.items = [tool_call_mock]
    chat_history_mock = MagicMock(spec=ChatHistory)

    kernel_mock = MagicMock(spec=Kernel)
    kernel_mock.invoke = AsyncMock(return_value="Function result")
    arguments = KernelArguments()

    chat_completion_base = OpenAIChatCompletionBase(
        ai_model_id="test_model_id", service_id="test", client=MagicMock(spec=AsyncOpenAI)
    )

    with patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.logger", autospec=True
    ) as logger_mock:
        await chat_completion_base._process_tool_calls(result_mock, kernel_mock, chat_history_mock, arguments)

    # logger_mock.info.assert_any_call(f"processing {len(result_mock.tool_calls)} tool calls")
    logger_mock.info.assert_any_call(f"Calling {tool_call_mock.name} function with args: {tool_call_mock.arguments}")

    kernel_mock.invoke.assert_called_once_with(**tool_call_mock.split_name_dict(), arguments={"arg_name": "arg_value"})

    chat_history_mock.add_message.assert_called_once()


@pytest.mark.asyncio
async def test_process_tool_calls_with_continuation_on_malformed_arguments():
    tool_call_mock = MagicMock(spec=FunctionCallContent)
    tool_call_mock.parse_arguments.side_effect = FunctionCallInvalidArgumentsException("Malformed arguments")
    tool_call_mock.name = "test_function"
    tool_call_mock.arguments = "Not a valid JSON string"
    tool_call_mock.id = "test_id"
    tool_call_mock.ai_model_id = None
    tool_call_mock.metadata = {}

    another_tool_call_mock = MagicMock(spec=FunctionCallContent)
    another_tool_call_mock.parse_arguments.return_value = {"another_arg_name": "another_arg_value"}
    another_tool_call_mock.name = "another_test_function"
    another_tool_call_mock.arguments = {"another_arg_name": "another_arg_value"}
    another_tool_call_mock.id = "another_test_id"
    another_tool_call_mock.ai_model_id = None
    another_tool_call_mock.metadata = {}

    result_mock = MagicMock(spec=ChatMessageContent)
    result_mock.items = [tool_call_mock, another_tool_call_mock]

    chat_history_mock = MagicMock(spec=ChatHistory)

    kernel_mock = MagicMock(spec=Kernel)
    kernel_mock.invoke = AsyncMock(return_value="Another Function result")

    arguments = KernelArguments()

    chat_completion_base = OpenAIChatCompletionBase(
        ai_model_id="test_model_id", service_id="test", client=MagicMock(spec=AsyncOpenAI)
    )

    with patch(
        "semantic_kernel.connectors.ai.open_ai.services.open_ai_chat_completion_base.logger", autospec=True
    ) as logger_mock:
        await chat_completion_base._process_tool_calls(result_mock, kernel_mock, chat_history_mock, arguments)

    logger_mock.exception.assert_any_call(
        "Received invalid arguments for function test_function: Malformed arguments. Trying tool call again."
    )

    add_message_calls = chat_history_mock.add_message.call_args_list
    assert any(
        call[1]["message"].items[0].result.text == "The tool call arguments are malformed, please try again."
        and call[1]["message"].items[0].id == "test_id"
        and call[1]["message"].items[0].name == "test_function"
        for call in add_message_calls
    ), "Expected call to add_message not found with the expected message content and metadata."


@pytest.mark.parametrize(
    "completions,tool_call_behavior,expected_result",
    [
        # Case 1: Empty completions, auto_invoke_kernel_functions=False
        ([], ToolCallBehavior(auto_invoke_kernel_functions=False), True),
        # Case 2: Completions with ChatMessageContent, auto_invoke_kernel_functions=True
        ([MagicMock(spec=ChatMessageContent)], ToolCallBehavior(auto_invoke_kernel_functions=True), True),
        # Case 3: Completions with ChatMessageContent, no tool_calls, auto_invoke_kernel_functions=True
        (
            [MagicMock(spec=ChatMessageContent, tool_calls=[])],
            ToolCallBehavior(auto_invoke_kernel_functions=True),
            True,
        ),
        # Case 4: Completions with StreamingChatMessageContent, auto_invoke_kernel_functions=True
        (
            [MagicMock(spec=StreamingChatMessageContent)],
            ToolCallBehavior(auto_invoke_kernel_functions=True),
            True,
        ),
        # Case 5: Completions with StreamingChatMessageContent, auto_invoke_kernel_functions=False
        (
            [MagicMock(spec=StreamingChatMessageContent)],
            ToolCallBehavior(auto_invoke_kernel_functions=False),
            True,
        ),
        # Case 6: Completions with both types, auto_invoke_kernel_functions=True
        (
            [MagicMock(spec=ChatMessageContent), MagicMock(spec=StreamingChatMessageContent)],
            ToolCallBehavior(auto_invoke_kernel_functions=True),
            True,
        ),
        # Case 7: Completions with ChatMessageContent with tool_calls, auto_invoke_kernel_functions=True
        (
            [MagicMock(spec=ChatMessageContent, tool_calls=[{}])],
            ToolCallBehavior(auto_invoke_kernel_functions=True),
            False,
        ),
    ],
)
@pytest.mark.asyncio
async def test_should_return_completions_response(completions, tool_call_behavior, expected_result):
    chat_completion_base = OpenAIChatCompletionBase(
        ai_model_id="test_model_id", service_id="test", client=MagicMock(spec=AsyncOpenAI)
    )
    result = chat_completion_base._should_return_completions_response(completions, tool_call_behavior)
    assert result == expected_result
