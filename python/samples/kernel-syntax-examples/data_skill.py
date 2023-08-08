# Copyright (c) Microsoft. All rights reserved.

import asyncio
import pandas as pd
import semantic_kernel as sk
import semantic_kernel.connectors.ai.open_ai as sk_oai
from semantic_kernel.core_skills import DataSkill

kernel = sk.Kernel()
api_key, org_id = sk.openai_settings_from_dot_env()
openai_chat_completion = sk_oai.OpenAIChatCompletion("gpt-3.5-turbo", api_key, org_id)
kernel.add_chat_service("chat_service", openai_chat_completion)

async def main() -> None:
    data = {
        "Name": ["Alice", "Bob", "Charlie", "David", "Eve"],
        "Age": [25, 32, 28, 22, 29],
        "City": ["New York", "Los Angeles", "Chicago", "Houston", "Miami"],
        "Salary": [60000, 75000, 52000, 48000, 67000],
    }
    df = pd.DataFrame(data)
    print(df)

    data_skill = DataSkill(data=df)
    kernel.import_skill(data_skill, skill_name="data")
    context = sk.ContextVariables()
    context["data_summary"] = data_skill.get_row_column_names()
    print(context["data_summary"])


if __name__ == "__main__":
    asyncio.run(main())