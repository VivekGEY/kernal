# Copyright (c) Microsoft. All rights reserved.

import pytest

from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.models.ai.chat_completion.chat_history import ChatHistory
from semantic_kernel.models.ai.chat_completion.chat_role import ChatRole


def test_init_with_system_message_only():
    system_msg = "test message"
    chat_history = ChatHistory(system_message=system_msg)
    assert len(chat_history.messages) == 1
    assert chat_history.messages[0].content == system_msg


def test_init_with_messages_only():
    msgs = [ChatMessageContent(role=ChatRole.USER, content=f"Message {i}") for i in range(3)]
    chat_history = ChatHistory(messages=msgs)
    assert chat_history.messages == msgs, "Chat history should contain exactly the provided messages"


def test_init_with_messages_and_system_message():
    system_msg = "a test system prompt"
    msgs = [ChatMessageContent(role=ChatRole.USER, content=f"Message {i}") for i in range(3)]
    chat_history = ChatHistory(messages=msgs, system_message=system_msg)
    assert chat_history.messages[0].role == ChatRole.SYSTEM, "System message should be the first in history"
    assert chat_history.messages[0].content == system_msg, "System message should be the first in history"
    assert chat_history.messages[1:] == msgs, "Remaining messages should follow the system message"


def test_init_without_messages_and_system_message():
    chat_history = ChatHistory()
    assert chat_history.messages == [], "Chat history should be empty if no messages and system_message are provided"


def test_add_system_message():
    chat_history = ChatHistory()
    content = "System message"
    chat_history.add_system_message(content)
    assert chat_history.messages[-1].content == content
    assert chat_history.messages[-1].role == ChatRole.SYSTEM


def test_add_system_message_at_init():
    chat_history = ChatHistory()
    content = "System message"
    chat_history = ChatHistory(system_message=content)
    assert chat_history.messages[-1].content == content
    assert chat_history.messages[-1].role == ChatRole.SYSTEM


def test_add_user_message():
    chat_history = ChatHistory()
    content = "User message"
    chat_history.add_user_message(content)
    assert chat_history.messages[-1].content == content
    assert chat_history.messages[-1].role == ChatRole.USER


def test_add_assistant_message():
    chat_history = ChatHistory()
    content = "Assistant message"
    chat_history.add_assistant_message(content)
    assert chat_history.messages[-1].content == content
    assert chat_history.messages[-1].role == ChatRole.ASSISTANT


def test_add_tool_message():
    chat_history = ChatHistory()
    content = "Tool message"
    chat_history.add_tool_message(content)
    assert chat_history.messages[-1].content == content
    assert chat_history.messages[-1].role == ChatRole.TOOL


def test_add_message():
    chat_history = ChatHistory()
    content = "Test message"
    role = ChatRole.USER
    encoding = "utf-8"
    chat_history.add_message(message={"role": role, "content": content}, encoding=encoding)
    assert chat_history.messages[-1].content == content
    assert chat_history.messages[-1].role == role
    assert chat_history.messages[-1].encoding == encoding


def test_remove_message():
    chat_history = ChatHistory()
    content = "Message to remove"
    role = ChatRole.USER
    encoding = "utf-8"
    message = ChatMessageContent(role=role, content=content, encoding=encoding)
    chat_history.messages.append(message)
    assert chat_history.remove_message(message) is True
    assert message not in chat_history.messages


def test_len():
    chat_history = ChatHistory()
    content = "Message"
    chat_history.add_user_message(content)
    chat_history.add_system_message(content)
    assert len(chat_history) == 2


def test_getitem():
    chat_history = ChatHistory()
    content = "Message for index"
    chat_history.add_user_message(content)
    assert chat_history[0].content == content


def test_contains():
    chat_history = ChatHistory()
    content = "Message to check"
    role = ChatRole.USER
    encoding = "utf-8"
    message = ChatMessageContent(role=role, content=content, encoding=encoding)
    chat_history.messages.append(message)
    assert message in chat_history


def test_iter():
    chat_history = ChatHistory()
    messages = ["Message 1", "Message 2"]
    for msg in messages:
        chat_history.add_user_message(msg)
    for i, message in enumerate(chat_history):
        assert message.content == messages[i]


def test_eq():
    # Create two instances of ChatHistory
    chat_history1 = ChatHistory()
    chat_history2 = ChatHistory()

    # Populate both instances with the same set of messages
    messages = [("Message 1", ChatRole.USER), ("Message 2", ChatRole.ASSISTANT)]
    for content, role in messages:
        chat_history1.add_message({"role": role, "content": content})
        chat_history2.add_message({"role": role, "content": content})

    # Assert that the two instances are considered equal
    assert chat_history1 == chat_history2

    # Additionally, test inequality by adding an extra message to one of the histories
    chat_history1.add_user_message("Extra message")
    assert chat_history1 != chat_history2


def test_serialize():
    system_msg = "a test system prompt"
    msgs = [ChatMessageContent(role=ChatRole.USER, content=f"Message {i}") for i in range(3)]
    chat_history = ChatHistory(messages=msgs, system_message=system_msg)
    json_str = chat_history.serialize()
    assert json_str is not None


def test_serialize_and_deserialize_to_chat_history():
    system_msg = "a test system prompt"
    msgs = [ChatMessageContent(role=ChatRole.USER, content=f"Message {i}") for i in range(3)]
    chat_history = ChatHistory(messages=msgs, system_message=system_msg)
    json_str = chat_history.serialize()
    new_chat_history = ChatHistory.restore_chat_history(json_str)
    assert new_chat_history == chat_history


def test_deserialize_invalid_json_raises_exception():
    invalid_json = "invalid json"

    with pytest.raises(ValueError):
        ChatHistory.restore_chat_history(invalid_json)
