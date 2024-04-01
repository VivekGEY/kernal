# Copyright (c) Microsoft. All rights reserved.

from typing import Any

from semantic_kernel.connectors.ai.open_ai.contents.open_ai_chat_message_content import OpenAIChatMessageContent
from semantic_kernel.contents.streaming_content_mixin import StreamingContentMixin
from semantic_kernel.exceptions import ContentAdditionException


class OpenAIStreamingChatMessageContent(StreamingContentMixin, OpenAIChatMessageContent):
    """This is the class for OpenAI streaming chat message response content.

    The end-user will have to either do something directly or gather them and combine them into a
    new instance.

    Args:
        choice_index: int - The index of the choice that generated this response.
        inner_content: ChatCompletionChunk - The inner content of the response,
            this should hold all the information from the response so even
            when not creating a subclass a developer can leverage the full thing.
        ai_model_id: Optional[str] - The id of the AI model that generated this response.
        metadata: Dict[str, Any] - Any metadata that should be attached to the response.
        role: Optional[ChatRole] - The role of the chat message, defaults to ASSISTANT.
        content: Optional[str] - The text of the response.
        encoding: Optional[str] - The encoding of the text.
        function_call: Optional[FunctionCall] - The function call that was generated by this response.
        tool_calls: Optional[List[ToolCall]] - The tool calls that were generated by this response.

    Methods:
        __str__: Returns the content of the response.
        __bytes__: Returns the content of the response encoded in the encoding.
        __add__: Combines two StreamingChatMessageContent instances.
    """

    def __bytes__(self) -> bytes:
        return self.content.encode(self.encoding if self.encoding else "utf-8") if self.content else b""

    def __add__(self, other: Any) -> "OpenAIStreamingChatMessageContent":
        """When combining two OpenAIStreamingChatMessageContent instances,
        the content fields are combined, as well as the arguments of the function or tool calls.

        The inner_content of the first one is used, ai_model_id and encoding should be the same,
        if role is set, they should be the same.
        """
        if not isinstance(other, OpenAIStreamingChatMessageContent):
            return self
        if self.choice_index != other.choice_index:
            raise ContentAdditionException("Cannot add StreamingChatMessageContent with different choice_index")
        if self.ai_model_id != other.ai_model_id:
            raise ContentAdditionException("Cannot add StreamingChatMessageContent from different ai_model_id")
        if self.encoding != other.encoding:
            raise ContentAdditionException("Cannot add StreamingChatMessageContent with different encoding")
        if self.role and other.role and self.role != other.role:
            raise ContentAdditionException("Cannot add StreamingChatMessageContent with different role")
        fc = (self.function_call + other.function_call) if self.function_call else other.function_call
        tc = {}
        if self.tool_calls:
            tc = {t.id: t for t in self.tool_calls}
            last_tc_id = list(tc.keys())[-1]
            if other.tool_calls:
                for new_tool in other.tool_calls:
                    if new_tool.id is None or new_tool.id == last_tc_id:
                        tc[last_tc_id] += new_tool
                    else:
                        tc[new_tool.id] = new_tool
        elif other.tool_calls:
            tc = {t.id: t for t in other.tool_calls}
        tc_list = list(tc.values())

        return OpenAIStreamingChatMessageContent(
            choice_index=self.choice_index,
            inner_content=self.inner_content,
            ai_model_id=self.ai_model_id,
            metadata=self.metadata,
            role=self.role,
            content=(self.content or "") + (other.content or ""),
            encoding=self.encoding,
            finish_reason=self.finish_reason or other.finish_reason,
            function_call=fc,
            tool_calls=tc_list,
            tool_call_id=self.tool_call_id or other.tool_call_id,
        )
