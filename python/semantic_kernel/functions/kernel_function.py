# Copyright (c) Microsoft. All rights reserved.

import asyncio
import logging
import platform
import sys
from functools import wraps
from inspect import isawaitable
from typing import TYPE_CHECKING, Any, AsyncIterable, Callable, ClassVar, Dict, List, Optional, Union

from pydantic import Field, StringConstraints

from semantic_kernel.kernel_pydantic import KernelBaseModel

if sys.version_info >= (3, 9):
    from typing import Annotated
else:
    from typing_extensions import Annotated

from semantic_kernel.connectors.ai.chat_completion_client_base import (
    ChatCompletionClientBase,
)
from semantic_kernel.connectors.ai.prompt_execution_settings import PromptExecutionSettings
from semantic_kernel.connectors.ai.text_completion_client_base import (
    TextCompletionClientBase,
)
from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.contents.streaming_kernel_content import StreamingKernelContent
from semantic_kernel.functions.function_result import FunctionResult
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.functions.kernel_function_metadata import KernelFunctionMetadata
from semantic_kernel.functions.kernel_parameter_metadata import KernelParameterMetadata
from semantic_kernel.models.ai.chat_completion.chat_history import ChatHistory
from semantic_kernel.prompt_template.chat_prompt_template import ChatPromptTemplate
from semantic_kernel.prompt_template.kernel_prompt_template import KernelPromptTemplate
from semantic_kernel.prompt_template.prompt_template_base import PromptTemplateBase
from semantic_kernel.prompt_template.prompt_template_config import PromptTemplateConfig
from semantic_kernel.utils.naming import generate_random_ascii_name

if TYPE_CHECKING:
    from semantic_kernel.functions.kernel_plugin_collection import KernelPluginCollection
    from semantic_kernel.kernel import Kernel

# TODO: is this needed anymore after sync code removal?
if platform.system() == "Windows" and sys.version_info >= (3, 8, 0):
    asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

logger: logging.Logger = logging.getLogger(__name__)


def store_results(chat_prompt: ChatPromptTemplate, results: List["ChatMessageContent"]):
    """Stores specific results in the context and chat prompt."""
    if hasattr(results[0], "tool_message") and results[0].tool_message is not None:
        chat_prompt.add_message(role="tool", message=results[0].tool_message)
    chat_prompt.add_message(
        "assistant",
        message=results[0].content,
        function_call=results[0].function_call if hasattr(results[0], "function_call") else None,
        tool_calls=results[0].tool_calls if hasattr(results[0], "tool_calls") else None,
    )
    return chat_prompt


class KernelFunction(KernelBaseModel):
    """
    Semantic Kernel function.

    Attributes:
        plugin_name (str): The name of the plugin that contains this function. Must be upper/lower
            case letters and underscores with a minimum length of 1.
        description (Optional[str]): The description of the function.
        name (str): The name of the function. Must be upper/lower case letters and
            underscores with a minimum length of 1.
        is_prompt (bool): Whether the function is semantic.
        stream_function (Optional[Callable[..., Any]]): The stream function for the function.
        parameters (List[KernelParameterMetadata]): The parameters for the function.
        return_parameter (Optional[KernelParameterMetadata]): The return parameter for the function.
        function (Callable[..., Any]): The function to call.
        plugins (Optional[KernelPluginCollection]): The collection of plugins.
        prompt_execution_settings (PromptExecutionSettings): The AI prompt execution settings.
        prompt_template_config (PromptTemplateConfig): The prompt template configuration.
        metadata (Optional[KernelFunctionMetadata]): The metadata for the function.

    Note: the CHAT_HISTORY_TAG is a class-level attribute that is used to tag the chat history in the
        arguments of the function. Pydantic ignores this attribute when performing model validation.
    """

    plugin_name: Annotated[str, StringConstraints(pattern=r"^[A-Za-z_]+$", min_length=1)]
    description: Optional[str] = Field(default=None)
    name: Annotated[str, StringConstraints(pattern=r"^[A-Za-z_]+$", min_length=1)]
    is_prompt: bool = Field(...)
    stream_function: Optional[Callable[..., Any]] = Field(default=None)
    parameters: List[KernelParameterMetadata] = Field(...)
    return_parameter: Optional[KernelParameterMetadata] = None
    function: Callable[..., Any] = Field(...)
    plugins: Optional["KernelPluginCollection"] = Field(default=None)
    prompt_execution_settings: Dict[str, PromptExecutionSettings] = Field(default_factory=dict)
    prompt_template_config: Optional[PromptTemplateConfig] = Field(default=PromptTemplateConfig)
    metadata: Optional[KernelFunctionMetadata] = Field(default=KernelFunctionMetadata)

    CHAT_HISTORY_TAG: ClassVar[str] = "chat_history"

    def __init__(
        self,
        function: Callable[..., Any],
        parameters: List[KernelParameterMetadata],
        description: str,
        plugin_name: str,
        function_name: str,
        is_prompt: bool,
        return_parameter: Optional[KernelParameterMetadata] = None,
        stream_function: Optional[Callable[..., Any]] = None,
        prompt_template_config: Optional[PromptTemplateConfig] = None,
        **kwargs: Dict[str, Any],
    ) -> None:
        """
        Initializes a new instance of the KernelFunction class

        Args:
            delegate_function (Callable[..., Any]): The delegate function for the function
            parameters (List[ParameterView]): The parameters for the function
            description (str): The description for the function
            plugin_name (str): The name of the plugin
            name (str): The name of the function
            is_prompt (bool): Whether the function is semantic
            delegate_stream_function (Optional[Callable[..., Any]]): The delegate stream function for the function
            kwargs (Dict[str, Any]): Additional keyword arguments
        """

        metadata = KernelFunctionMetadata(
            name=function_name,
            description=description,
            parameters=parameters,
            return_parameter=return_parameter,
            is_prompt=is_prompt,
            plugin_name=plugin_name,
        )

        super().__init__(
            function=function,
            parameters=parameters,
            return_parameter=return_parameter,
            description=description,
            plugin_name=plugin_name,
            name=function_name,
            is_prompt=is_prompt,
            stream_function=stream_function,
            prompt_template_config=prompt_template_config,
            metadata=metadata,
            **kwargs,
        )

    @property
    def metadata(self) -> KernelFunctionMetadata:
        return self.describe()

    @staticmethod
    def from_native_method(method: Callable[..., Any], plugin_name: str) -> "KernelFunction":
        """
        Create a KernelFunction from a native method.

        Args:
            method (Callable[..., Any]): The method to create the function from
            plugin_name (str): The name of the plugin

        Returns:
            KernelFunction: The kernel function
        """
        if method is None:
            raise ValueError("Method cannot be `None`")

        if not hasattr(method, "__kernel_function__") or method.__kernel_function__ is None:
            raise ValueError("Method is not a Kernel function")

        parameters = []
        if hasattr(method, "__kernel_function_context_parameters__"):
            for param in method.__kernel_function_context_parameters__:
                assert "name" in param, "Parameter name is empty"
                assert "description" in param, "Parameter description is empty"
                assert "default_value" in param, "Parameter default value is empty"

                parameters.append(
                    KernelParameterMetadata(
                        name=param["name"],
                        description=param["description"],
                        default_value=param["default_value"],
                        type=param.get("type", "str"),
                        required=param.get("required", False),
                        expose=True,
                    )
                )
        return_param = KernelParameterMetadata(
            name="return",
            description=method.__kernel_function_return_description__
            if hasattr(method, "__kernel_function_return_description__")
            else "",
            default_value=None,
            type=method.__kernel_function_return_type__
            if hasattr(method, "__kernel_function_return_type__")
            else "None",
            required=method.__kernel_function_return_required__
            if hasattr(method, "__kernel_function_return_required__")
            else False,
        )

        function_name = method.__kernel_function_name__
        description = method.__kernel_function_description__

        if hasattr(method, "__kernel_function_streaming__") and method.__kernel_function_streaming__:
            streaming_method = method

            @wraps(method)
            async def _non_streaming_function(*args, **kwargs):
                return [x async for x in streaming_method(*args, **kwargs)]

            method = _non_streaming_function
        else:
            streaming_method = None

        return KernelFunction(
            function=method,
            function_name=function_name,
            plugin_name=plugin_name,
            description=description,
            parameters=parameters,
            return_parameter=return_param,
            stream_function=streaming_method,
            is_prompt=False,
        )

    @staticmethod
    def from_prompt(
        prompt: str,
        execution_settings: Optional[PromptExecutionSettings] = None,
        plugin_name: Optional[str] = None,
        function_name: Optional[str] = None,
        description: Optional[str] = None,
        template_format: Optional[str] = None,
        prompt_template: Optional[PromptTemplateBase] = None,
        prompt_template_config: Optional[PromptTemplateConfig] = None,
    ) -> "KernelFunction":
        """
        Create a Kernel Function from a prompt

        Args:
            prompt (str): The prompt
            execution_settings (Optional[PromptExecutionSettings]): The execution settings
            plugin_name (Optional[str]): The name of the plugin
            function_name (Optional[str]): The name of the function
            description (Optional[str]): The description of the function
            template_format (Optional[str]): The template format
            prompt_template (Optional[PromptTemplateBase]): The prompt template
            prompt_template_config (Optional[PromptTemplateConfig]): The prompt template configuration

        Returns:
            KernelFunction: The kernel function
        """

        if prompt_template:
            if not template_format:
                raise ValueError(f"Template format cannot be `None` when providing a {prompt_template}")

        if not plugin_name:
            plugin_name = f"p_{generate_random_ascii_name()}"

        if not prompt_template_config:
            prompt_template_config = PromptTemplateConfig(
                name=function_name,
                template_format=template_format if template_format else "semantic-kernel",
                description=description if description else "Generic function, unknown purpose",
                template=prompt,
                execution_settings=execution_settings if execution_settings else PromptExecutionSettings(),
            )

        if not prompt_template:
            prompt_template = KernelPromptTemplate(prompt_template_config)

        async def _local_func(
            function: KernelFunctionMetadata,
            kernel: "Kernel",
            service: Union[TextCompletionClientBase, ChatCompletionClientBase],
            request_settings: PromptExecutionSettings,
            arguments: KernelArguments,
            chat_history: Optional[ChatHistory] = None,
            **kwargs: Dict[str, Any],
        ) -> "FunctionResult":
            if service is None:
                raise ValueError("AI LLM service cannot be `None`")

            prompt = await prompt_template.render(kernel, arguments)

            if not chat_history or len(chat_history) == 0:
                chat_history = ChatHistory(system_message=prompt)
            else:
                chat_history.add_user_message(prompt)

            try:
                if isinstance(service, ChatCompletionClientBase):
                    completions = await service.complete_chat(chat_history, request_settings)
                    return FunctionResult(
                        function=function,
                        value=completions,
                        metadata={
                            "messages": chat_history,
                            "arguments": arguments,
                            "metadata": [completion.metadata for completion in completions],
                        },
                    )
            except Exception as exc:
                logger.error(f"Error occurred while invoking function {function.name}: {exc}")
                raise exc

            try:
                if isinstance(service, TextCompletionClientBase):
                    completions = await service.complete(prompt, request_settings)
                    return FunctionResult(
                        function=function,
                        value=completions,
                        metadata={
                            "prompt": prompt,
                            "arguments": arguments,
                            "metadata": [completion.metadata for completion in completions],
                        },
                    )
            except Exception as e:
                logger.error(f"Error occurred while invoking function {function.name}: {e}")
                raise e

            raise ValueError(f"Service `{type(service)}` is not a valid AI service")

        async def _local_stream_func(
            function: KernelFunctionMetadata,
            kernel: "Kernel",
            service: Union[TextCompletionClientBase, ChatCompletionClientBase],
            request_settings: PromptExecutionSettings,
            arguments: KernelArguments,
            chat_history: Optional[ChatHistory] = None,
            **kwargs: Dict[str, Any],
        ) -> AsyncIterable[Union[FunctionResult, List[StreamingKernelContent]]]:
            if service is None:
                raise ValueError("AI LLM service cannot be `None`")

            prompt = await prompt_template.render(kernel, arguments)

            if not chat_history or len(chat_history) == 0:
                chat_history = ChatHistory(system_message=prompt)
            else:
                chat_history.add_user_message(prompt)

            try:
                if isinstance(service, ChatCompletionClientBase):
                    async for partial_content in service.complete_chat_stream(
                        chat_history=chat_history, settings=request_settings
                    ):
                        yield partial_content
                elif isinstance(service, TextCompletionClientBase):
                    async for partial_content in service.complete_stream(prompt, request_settings):
                        yield partial_content
                else:
                    raise ValueError(f"Service `{type(service)}` is not a valid AI service")
            except Exception as e:
                logger.error(f"Error occurred while invoking function {function.name}: {e}")
                raise e

        semantic_function_params = [
            KernelParameterMetadata(
                name="function",
                description="The function to execute",
                default_value=None,
                type="KernelFunctionMetadata",
                required=True,
                expose=False,
            ),
            KernelParameterMetadata(
                name="kernel",
                description="The kernel",
                default_value=None,
                type="Kernel",
                required=True,
                expose=False,
            ),
            KernelParameterMetadata(
                name="service",
                description="The AI service client",
                default_value=None,
                type="AIServiceClientBase",
                required=True,
                expose=False,
            ),
            KernelParameterMetadata(
                name="request_settings",
                description="The request settings",
                default_value=None,
                type="PromptExecutionSettings",
                required=True,
                expose=False,
            ),
            KernelParameterMetadata(
                name="arguments",
                description="The kernel arguments",
                default_value=None,
                type="KernelArguments",
                required=True,
                expose=False,
            ),
        ]
        semantic_function_params.extend(prompt_template_config.get_kernel_parameter_metadata())
        return KernelFunction(
            function_name=function_name,
            plugin_name=plugin_name,
            description=description,
            function=_local_func,
            parameters=semantic_function_params,
            return_parameter=KernelParameterMetadata(
                name="return",
                description="The completion result",
                default_value=None,
                type="FunctionResult",
                required=True,
            ),
            stream_function=_local_stream_func,
            is_prompt=True,
            prompt_template_config=prompt_template_config,
        )

    def set_default_plugin_collection(self, plugins: "KernelPluginCollection") -> "KernelFunction":
        self.plugins = plugins
        return self

    def describe(self) -> KernelFunctionMetadata:
        return KernelFunctionMetadata(
            name=self.name,
            plugin_name=self.plugin_name,
            description=self.description or "",
            is_prompt=self.is_prompt,
            parameters=self.parameters,
        )

    async def __call__(
        self,
        kernel: "Kernel",
        arguments: Optional[KernelArguments] = None,
        **kwargs: Dict[str, Any],
    ) -> "FunctionResult":
        """Invoke the function with the given arguments.

        Args:
            kernel (Kernel): The kernel
            arguments (Optional[KernelArguments]): The Kernel arguments.
                Optional, defaults to None.
            kwargs (Dict[str, Any]): Additional keyword arguments that will be

        Returns:
            FunctionResult: The result of the function
        """
        if not arguments:
            arguments = KernelArguments(**kwargs)
        return await self.invoke(kernel, arguments)

    async def invoke(
        self,
        kernel: "Kernel",
        arguments: Optional[KernelArguments] = None,
        **kwargs: Dict[str, Any],
    ) -> "FunctionResult":
        """Invoke the function with the given arguments.

        Args:
            kernel (Kernel): The kernel
            arguments (KernelArguments): The Kernel arguments
            kwargs (Dict[str, Any]): Additional keyword arguments that will be
                added to the KernelArguments.

        Returns:
            FunctionResult: The result of the function
        """
        if not arguments:
            arguments = KernelArguments(**kwargs)
        function_arguments = self.gather_function_parameters(kernel, arguments)
        if self.is_prompt and self.CHAT_HISTORY_TAG not in function_arguments:
            function_arguments[self.CHAT_HISTORY_TAG] = ChatHistory()
        logger.debug("Invoking %s with arguments: %s", self.name, function_arguments)
        try:
            result = self.function(**function_arguments)
            if isawaitable(result):
                result = await result
        except Exception as exc:
            logger.error(f"Error occurred while invoking function {self.name}: {exc}")
            return FunctionResult(
                function=self.describe(), value=None, metadata={"error": exc, "arguments": function_arguments}
            )
        logger.debug("Function result: %s", result)
        logger.debug("Function result type %s", type(result))
        if self.return_parameter and self.return_parameter.type_ and "FunctionResult" in self.return_parameter.type_:
            return result
        return FunctionResult(function=self.describe(), value=result, metadata={"arguments": function_arguments})

    async def invoke_stream(
        self,
        kernel: "Kernel",
        arguments: Optional[KernelArguments] = None,
        **kwargs: Dict[str, Any],
    ) -> AsyncIterable[Union[FunctionResult, List[Union[StreamingKernelContent, Any]]]]:
        """
        Invoke a stream async function with the given arguments.

        Args:
            kernel (Kernel): The kernel
            arguments (KernelArguments): The Kernel arguments
            kwargs (Dict[str, Any]): Additional keyword arguments that will be
                added to the KernelArguments.

        Yields:
            StreamingKernelContent or FunctionResult -- The results of the function,
                if there is an error a FunctionResult is yielded.
        """
        if not arguments:
            arguments = KernelArguments(**kwargs)
        if not self.stream_function:
            raise ValueError("Function does not support streaming")
        function_arguments = self.gather_function_parameters(kernel, arguments)
        if self.is_prompt and self.CHAT_HISTORY_TAG not in function_arguments:
            function_arguments[self.CHAT_HISTORY_TAG] = ChatHistory()
        logger.debug("Invoking %s with arguments: %s", self.name, function_arguments)
        try:
            async for stream_msg in self.stream_function(**function_arguments):
                yield stream_msg
        except Exception as e:
            logger.error(f"Error occurred while invoking function {self.name}: {e}")
            yield FunctionResult(
                function=self.describe(), value=None, metadata={"error": e, "arguments": function_arguments}
            )

    def gather_function_parameters(self, kernel: "Kernel", arguments: "KernelArguments") -> Dict[str, Any]:
        """Gathers the function parameters from the arguments."""
        function_arguments: Dict[str, Any] = {}
        for param in self.parameters:
            if param.name == "function":
                function_arguments[param.name] = self.describe()
                continue
            if param.name == "kernel":
                function_arguments[param.name] = kernel
                continue
            if param.name == "service":
                function_arguments[param.name] = kernel.select_ai_service(self, arguments)[0]
                continue
            if param.name == "request_settings":
                function_arguments[param.name] = kernel.select_ai_service(self, arguments)[1]
                continue
            if param.name == "arguments":
                function_arguments[param.name] = arguments
                continue
            if param.name == "prompt_template_config":
                function_arguments[param.name] = self.prompt_template_config
                continue
            if param.name == self.CHAT_HISTORY_TAG:
                chat = arguments.get(self.CHAT_HISTORY_TAG, ChatHistory())
                if not isinstance(chat, ChatHistory):
                    raise ValueError(f"Parameter {param.name} is not a valid ChatHistory object.")
                function_arguments[param.name] = chat
                continue
            if self.is_prompt:
                # a semantic function will use the arguments (KernelArguments) instead of named arguments
                continue
            if param.name in arguments:
                function_arguments[param.name] = arguments[param.name]
                continue
            if param.required:
                raise ValueError(f"Parameter {param.name} is required but not provided in the arguments.")
            logger.debug(f"Parameter {param.name} is not provided, using default value {param.default_value}")
        if self.prompt_template_config:
            self.add_default_values(function_arguments, self.prompt_template_config)
        return function_arguments

    def add_default_values(self, arguments: dict[str, Any], prompt_template_config: PromptTemplateConfig) -> None:
        """Adds default values to the arguments."""
        for parameter in prompt_template_config.input_variables:
            if not arguments.get(parameter.name) and parameter.default not in {None, "", False, 0}:
                arguments[parameter.name] = parameter.default
