# Copyright (c) Microsoft. All rights reserved.
from typing import Optional

from semantic_kernel.connectors.ai.open_ai.contents.open_ai_streaming_chat_message_content import (
    OpenAIStreamingChatMessageContent,
)


class AzureStreamingChatMessageContent(OpenAIStreamingChatMessageContent):
    """This is the class for Azure OpenAI streaming chat message response content.

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
        tool_message: Optional[str] - The content of the tool message generated by the extentions API.

    Methods:
        __str__: Returns the content of the response.
        __bytes__: Returns the content of the response encoded in the encoding.
        __dict__: Returns a dict representation of the response, with role and content fields.
        __add__: Combines two StreamingChatMessageContent instances.
    """

    tool_message: Optional[str] = None

    def __add__(self, other: "AzureStreamingChatMessageContent") -> "AzureStreamingChatMessageContent":
        """When combining two AzureOpenAIStreamingChatMessageContent instances,
        the content fields are combined, as well as the arguments of the function or tool calls.

        The inner_content of the first one is used, ai_model_id and encoding should be the same,
        if role is set, they should be the same.
        """
        if self.choice_index != other.choice_index:
            raise ValueError("Cannot add StreamingChatMessageContent with different choice_index")
        if self.ai_model_id != other.ai_model_id:
            raise ValueError("Cannot add StreamingChatMessageContent from different ai_model_id")
        if self.encoding != other.encoding:
            raise ValueError("Cannot add StreamingChatMessageContent with different encoding")
        if self.role and other.role and self.role != other.role:
            raise ValueError("Cannot add StreamingChatMessageContent with different role")
        fc = (self.function_call + other.function_call) if self.function_call else other.function_call
        if self.tool_calls:
            tc = []
            for index, tool in self.tool_calls:
                if other.tool_calls:
                    tc.append(tool + other.tool_calls[index])
                else:
                    tc.append(tool)
        else:
            tc = other.tool_calls

        return AzureStreamingChatMessageContent(
            choice_index=self.choice_index,
            inner_content=self.inner_content,
            ai_model_id=self.ai_model_id,
            metadata=self.metadata,
            role=self.role,
            content=(self.content or "") + (other.content or ""),
            encoding=self.encoding,
            finish_reason=self.finish_reason or other.finish_reason,
            function_call=fc,
            tool_calls=tc,
            tool_message=(self.tool_message or "") + (other.tool_message or ""),
        )
