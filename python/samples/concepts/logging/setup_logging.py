# Copyright (c) Microsoft. All rights reserved.

import logging
import os

from semantic_kernel import Kernel
from semantic_kernel.connectors.ai.open_ai import OpenAIChatCompletion
from semantic_kernel.utils.logging import setup_logging
from semantic_kernel.utils.settings import openai_settings_from_dot_env


async def main():
    setup_logging()

    # Set the logging level for  semantic_kernel.kernel to DEBUG.
    logging.getLogger("kernel").setLevel(logging.DEBUG)

    kernel = Kernel()

    api_key, org_id = openai_settings_from_dot_env()

    service_id = "chat-gpt"
    kernel.add_service(
        OpenAIChatCompletion(service_id=service_id, ai_model_id="gpt-3.5-turbo", api_key=api_key, org_id=org_id)
    )

    plugins_directory = os.path.join(__file__, "../../../../../prompt_template_samples/")
    plugin = kernel.add_plugin(parent_directory=plugins_directory, plugin_name="FunPlugin")

    joke_function = plugin["Joke"]

    result = await kernel.invoke(joke_function, input="time travel to dinosaur age", style="silly")

    print(result)


if __name__ == "__main__":
    import asyncio

    asyncio.run(main())
