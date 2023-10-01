# Copyright (c) Microsoft. All rights reserved.

import json
from logging import Logger
from typing import Any, List, Optional, Union

import aiohttp

from semantic_kernel.connectors.ai.ai_exception import AIException
from semantic_kernel.connectors.ai.complete_request_settings import (
    CompleteRequestSettings,
)
from semantic_kernel.connectors.ai.text_completion_client_base import (
    TextCompletionClientBase,
)
from semantic_kernel.utils.null_logger import NullLogger


class OllamaTextCompletion(TextCompletionClientBase):
    _model_id: str
    _api_version: str
    _base_url: str
    _log: Logger
    _prompt_tokens: int
    _completion_tokens: int
    _total_tokens: int

    def __init__(
        self,
        model_id: str,
        api_version: Optional[str] = None,
        base_url: Optional[str] = None,
        log: Optional[Logger] = None,
    ) -> None:
        """
        Initializes a new instance of the OpenAITextCompletion class.

        Arguments:
            model_id {str} -- The Ollama model name, see
                https://ollama.ai/library
        """
        self._model_id = model_id
        self._api_version = api_version or '0.1.0'
        self._base_url = base_url or 'http://127.0.0.1:11434'
        self._log = log if log is not None else NullLogger()

    async def complete_async(
        self,
        prompt: str,
        request_settings: CompleteRequestSettings,
        logger: Optional[Logger] = None,
    ) -> Union[str, List[str]]:
        # TODO Support choices/number_of_responses.
        assert request_settings.number_of_responses == 1
        result = ""
        response = self._send_completion_request(prompt, request_settings, logger)
        async for c in response:
            result += c
        return result


    async def complete_stream_async(
        self,
        prompt: str,
        request_settings: CompleteRequestSettings,
        logger: Optional[Logger] = None,
    ):
        response = self._send_completion_request(prompt, request_settings, logger)

        async for chunk in response:
            if request_settings.number_of_responses > 1:
                # TODO Support choices/number_of_responses.
                for choice in chunk.choices:
                    completions = [""] * request_settings.number_of_responses
                    completions[choice.index] = choice.text
                    yield completions
            else:
                yield chunk

    async def _send_completion_request(
        self,
        prompt: str,
        request_settings: CompleteRequestSettings,
        logger: Optional[Logger] = None
    ):
        """
        Completes the given prompt. Returns a single string completion.
        Cannot return multiple completions. Cannot return logprobs.

        Arguments:
            prompt {str} -- The prompt to complete.
            request_settings {CompleteRequestSettings} -- The request settings.

        Returns:
            str -- The completed text.
        """
        logger = logger or self._log
        if self._api_version != '0.1.0':
            raise ValueError(f"Unsupported Ollama API version: {self._api_version}. Only 0.1.0 is supported.")
        if not prompt:
            raise ValueError("The prompt cannot be `None` or empty")
        if request_settings is None:
            raise ValueError("The request settings cannot be `None`")

        if request_settings.max_tokens < 1:
            raise AIException(
                AIException.ErrorCodes.InvalidRequest,
                "The max tokens must be greater than 0, "
                f"but was {request_settings.max_tokens}",
            )

        if request_settings.logprobs != 0:
            raise AIException(
                AIException.ErrorCodes.InvalidRequest,
                "complete_async does not support logprobs, "
                f"but logprobs={request_settings.logprobs} was requested",
            )

        # TODO Set up other custom parameters defined at https://github.com/jmorganca/ollama/blob/main/docs/api.md.
        request = dict(
            model=self._model_id,
            prompt=prompt,
        )

        try:
            # Only streaming is supported.
            async with aiohttp.ClientSession() as session:
                async with session.post(f'{self._base_url}/api/generate', json=request) as r:
                    async for chunk in r.content:
                        token_info = json.loads(chunk.decode('utf-8'))
                        if token_info['done']:
                            # Can't use "%s" because we need to be compatible with `NullLogger`.
                            logger.debug(f"Ollama response: {token_info}")
                            break
                        token = token_info['response']
                        yield token
        except Exception as ex:
            raise AIException(
                AIException.ErrorCodes.ServiceError,
                "Ollama service failed to complete the prompt.",
                ex,
            )


# TODO Move to a test after trying it.
if __name__ == '__main__':
    o = OllamaTextCompletion('orca-mini')
    import asyncio

    async def main():
        print("streaming: tokens:")
        r = o.complete_stream_async("Hello.\n", CompleteRequestSettings())
        async for item in r:
            print(item)

        print("\nnot streaming:")
        r = await o.complete_async("Hello.\n", CompleteRequestSettings())
        print(r)

    asyncio.run(main())
