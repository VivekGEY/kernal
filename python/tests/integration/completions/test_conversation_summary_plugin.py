# Copyright (c) Microsoft. All rights reserved.

import os

import pytest
from test_utils import retry

import semantic_kernel as sk
import semantic_kernel.connectors.ai.open_ai as sk_oai
from semantic_kernel.core_plugins.conversation_summary_plugin import (
    ConversationSummaryPlugin,
)
from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.functions.kernel_arguments import KernelArguments

@pytest.mark.asyncio
async def test_azure_summarize_conversation_using_plugin(setup_summarize_conversation_using_plugin, get_aoai_config):
    kernel, chatTranscript = setup_summarize_conversation_using_plugin

    if "Python_Integration_Tests" in os.environ:
        deployment_name = os.environ["AzureOpenAI__DeploymentName"]
        api_key = os.environ["AzureOpenAI__ApiKey"]
        endpoint = os.environ["AzureOpenAI__Endpoint"]
    else:
        # Load credentials from .env file
        deployment_name, api_key, endpoint = get_aoai_config
        deployment_name = "gpt-35-turbo-instruct"

    kernel.add_text_completion_service(
        "text_completion",
        sk_oai.AzureTextCompletion(deployment_name=deployment_name, endpoint=endpoint, api_key=api_key),
    )

    conversationSummaryPlugin = kernel.import_plugin(ConversationSummaryPlugin(kernel), "conversationSummary")

    exec_settings = PromptExecutionSettings(
        extension_data = { "max_tokens": 200, "temperature": 0, "top_p": 0.5}
    )

    prompt_template_config = PromptTemplateConfig(
        template=prompt,
        description="Write a short story.",
        execution_settings={'default': exec_settings}
    )

    # Create the semantic function
    tldr_function = kernel.create_function_from_prompt(prompt_template_config=prompt_template_config)

    arguments = KernelArguments(input=chatTranscript)

    summary = await retry(lambda: kernel.invoke(conversationSummaryPlugin["SummarizeConversation"], arguments))

    # summary = await retry(
    #     lambda: kernel.invoke(conversationSummaryPlugin["SummarizeConversation"], input_str=chatTranscript)
    # )

    output = str(summary).strip().lower()
    print(output)
    assert "john" in output and "jane" in output
    assert len(output) < len(chatTranscript)


@pytest.mark.asyncio
@pytest.mark.xfail(reason="This test fails intermittently when run in parallel with other tests")
async def test_oai_summarize_conversation_using_plugin(
    setup_summarize_conversation_using_plugin,
):
    _, chatTranscript = setup_summarize_conversation_using_plugin

    # Defining a new kernel here to avoid using the same kernel as the previous test
    # which causes failures.
    kernel = sk.Kernel()

    if "Python_Integration_Tests" in os.environ:
        api_key = os.environ["OpenAI__ApiKey"]
        org_id = None
    else:
        # Load credentials from .env file
        api_key, org_id = sk.openai_settings_from_dot_env()

    kernel.add_text_completion_service(
        "davinci-003",
        sk_oai.OpenAITextCompletion("gpt-3.5-turbo-instruct", api_key, org_id=org_id),
    )

    conversationSummaryPlugin = kernel.import_plugin(ConversationSummaryPlugin(kernel), "conversationSummary")

    summary = await retry(
        lambda: kernel.run(conversationSummaryPlugin["SummarizeConversation"], input_str=chatTranscript)
    )

    output = str(summary).strip().lower()
    print(output)
    assert "john" in output and "jane" in output
    assert len(output) < len(chatTranscript)
