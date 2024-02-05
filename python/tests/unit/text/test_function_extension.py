import pytest

import semantic_kernel.connectors.ai.open_ai as sk_oai
from semantic_kernel import Kernel
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.text import aggregate_chunked_results


@pytest.mark.asyncio
async def test_aggregate_results():
    kernel = Kernel()
    kernel.add_text_completion_service("davinci-002", sk_oai.OpenAITextCompletion("text-davinci-002", "none", "none"))
    sk_prompt = """
        {{$input}}
        How is that ?
    """

    func = kernel.create_semantic_function(
        sk_prompt,
        max_tokens=200,
        temperature=0,
        top_p=0.5,
    )

    chunked = [
        "This is a test of the emergency broadcast system.",
        "This is only a test",
        "We repeat, this is only a test? A unit test",
        "A small note! And another? And once again!",
        "Seriously, this is the end.",
        "We're finished. All set. Bye. Done",
    ]
    result = await aggregate_chunked_results(func, chunked, kernel, KernelArguments())
    print(result)
    assert result == "\n".join(chunked)
