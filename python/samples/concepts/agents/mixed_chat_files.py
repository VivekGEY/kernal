# Copyright (c) Microsoft. All rights reserved.

import asyncio
import os

from semantic_kernel.agents import AgentGroupChat, ChatCompletionAgent
from semantic_kernel.agents.open_ai import OpenAIAssistantAgent
from semantic_kernel.agents.open_ai.azure_assistant_agent import AzureAssistantAgent
from semantic_kernel.agents.strategies.termination.termination_strategy import TerminationStrategy
from semantic_kernel.connectors.ai.open_ai.services.azure_chat_completion import AzureChatCompletion
from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.contents.utils.author_role import AuthorRole
from semantic_kernel.kernel import Kernel

#####################################################################
# The following sample demonstrates how to create an OpenAI         #
# assistant using either Azure OpenAI or OpenAI, a chat completion  #
# agent and have them participate in a group chat working on        #
# an uploaded file.                                                 #
#####################################################################


class ApprovalTerminationStrategy(TerminationStrategy):
    """A strategy for determining when an agent should terminate."""

    async def should_agent_terminate(self, agent, history):
        """Check if the agent should terminate."""
        return "approved" in history[-1].content.lower()


SUMMARY_INSTRUCTIONS = "Summarize the entire conversation for the user in natural language."


def _create_kernel_with_chat_completion(service_id: str) -> Kernel:
    kernel = Kernel()
    kernel.add_service(AzureChatCompletion(service_id=service_id))
    # kernel.add_service(OpenAIChatCompletion(service_id=service_id))
    return kernel


async def invoke_agent(
    chat: AgentGroupChat, agent: ChatCompletionAgent | OpenAIAssistantAgent, input: str | None = None
) -> None:
    """Invoke the agent with the user input."""
    if input:
        await chat.add_chat_message(message=ChatMessageContent(role=AuthorRole.USER, content=input))
        print(f"# {AuthorRole.USER}: '{input}'")

    async for content in chat.invoke(agent=agent):
        print(f"# {content.role} - {content.name or '*'}: '{content.content}'")


async def main():
    try:
        file_path = os.path.join(
            os.path.dirname(os.path.dirname(os.path.realpath(__file__))),
            "resources",
            "mixed_chat_files",
            "user-context.txt",
        )

        analyst_agent = await AzureAssistantAgent.create(
            service_id="analyst",
            kernel=Kernel(),
            enable_code_interpreter=True,
            code_interpreter_filenames=[file_path],
            name="AnalystAgent",
        )

        service_id = "summary"
        summary_agent = ChatCompletionAgent(
            service_id=service_id,
            kernel=_create_kernel_with_chat_completion(service_id=service_id),
            instructions=SUMMARY_INSTRUCTIONS,
            name="SummaryAgent",
        )

        chat = AgentGroupChat()

        await invoke_agent(
            chat=chat,
            agent=analyst_agent,
            input="""
            Create a tab delimited file report of the ordered (descending) frequency distribution
            of words in the file 'user-context.txt' for any words used more than once.
            """,
        )
        await invoke_agent(chat=chat, agent=summary_agent)
    finally:
        [await analyst_agent.delete_file(file_id=file_id) for file_id in analyst_agent.code_interpreter_file_ids]
        await analyst_agent.delete()


if __name__ == "__main__":
    asyncio.run(main())
