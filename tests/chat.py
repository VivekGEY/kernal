# Copyright (c) Microsoft. All rights reserved.

import asyncio

import semantic_kernel as sk

kernel = sk.create_kernel()

api_key, org_id = sk.openai_settings_from_dot_env()

kernel.config.add_openai_completion_backend(
    "davinci-003", "text-davinci-003", api_key, org_id
)

sk_prompt = """
ChatBot can have a conversation with you about any topic.
It can give explicit instructions or say 'I don't know'
when it doesn't know the answer.

{{$chat_history}}
Human:>{{$human_input}}
ChatBot:>
""".strip()

prompt_config = sk.PromptTemplateConfig.from_completion_parameters(
    max_tokens=2000,
    temperature=0.7,
    top_p=0.4
)

prompt_template = sk.PromptTemplate(
    sk_prompt, kernel.prompt_template_engine, prompt_config
)
function_config = sk.SemanticFunctionConfig(prompt_config, prompt_template)
chat_function = kernel.register_semantic_function("ChatBot", "Chat", function_config)

context = sk.ContextVariables()
context["chat_history"] = ""


async def chat() -> None:
    human_input = input("Human:>")
    context["human_input"] = human_input

    answer = await kernel.run_on_vars_async(context, chat_function)
    context["chat_history"] += f"\nHuman:>{human_input}\nChatBot:>{answer}\n"

    print(answer)


async def main() -> None:
    while True:
        await chat()


if __name__ == "__main__":
    asyncio.run(main())
