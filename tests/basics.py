import asyncio

import semantic_kernel as sk

kernel = sk.create_kernel()

api_key, org_id = sk.openai_settings_from_dot_env()

kernel.config.add_openai_completion_backend(
    "davinci-003", "text-davinci-003", api_key, org_id
)

sk_prompt = """
{{$input}}

Give me the TLDR in 5 words.
"""

text_to_summarize = """
    1) A robot may not injure a human being or, through inaction,
    allow a human being to come to harm.

    2) A robot must obey orders given it by human beings except where
    such orders would conflict with the First Law.

    3) A robot must protect its own existence as long as such protection
    does not conflict with the First or Second Law.
"""

tldr_function = sk.extensions.create_semantic_function(
    kernel,
    sk_prompt,
    max_tokens=200,
    temperature=0,
    top_p=0.5,
)

print("Summarizing: ")
print(text_to_summarize)
print()

summary = asyncio.run(kernel.run_on_str_async(text_to_summarize, tldr_function))
output = str(summary.variables).strip()

print(f"Summary is: '{output}'")
