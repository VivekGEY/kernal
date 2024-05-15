# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

import logging
from inspect import isasyncgen, isasyncgenfunction, isawaitable, iscoroutinefunction, isgenerator, isgeneratorfunction
from typing import TYPE_CHECKING, Any, AsyncGenerator, Callable

from pydantic import ValidationError

from semantic_kernel.contents.streaming_content_mixin import StreamingContentMixin
from semantic_kernel.exceptions import FunctionExecutionException, FunctionInitializationError
from semantic_kernel.filters.function.function_context import FunctionContext
from semantic_kernel.functions.function_result import FunctionResult
from semantic_kernel.functions.kernel_arguments import KernelArguments
from semantic_kernel.functions.kernel_function import KernelFunction
from semantic_kernel.functions.kernel_function_metadata import KernelFunctionMetadata
from semantic_kernel.functions.kernel_parameter_metadata import KernelParameterMetadata

if TYPE_CHECKING:
    from semantic_kernel.kernel import Kernel


logger: logging.Logger = logging.getLogger(__name__)


class KernelFunctionFromMethod(KernelFunction):
    """Semantic Kernel Function from a method."""

    # some attributes are now properties, still listed here for documentation purposes

    method: Callable[..., Any]
    stream_method: Callable[..., Any] | None = None

    def __init__(
        self,
        method: Callable[..., Any],
        plugin_name: str | None = None,
        stream_method: Callable[..., Any] | None = None,
    ) -> None:
        """
        Initializes a new instance of the KernelFunctionFromMethod class

        Args:
            method (Callable[..., Any]): The method to be called
            plugin_name (Optional[str]): The name of the plugin
            stream_method (Optional[Callable[..., Any]]): The stream method for the function
        """
        if method is None:
            raise FunctionInitializationError("Method cannot be `None`")

        if not hasattr(method, "__kernel_function__") or method.__kernel_function__ is None:
            raise FunctionInitializationError("Method is not a Kernel function")

        # all these fields are created when the kernel function decorator is used,
        # so no need to check before using, will raise an exception if not set
        function_name = method.__kernel_function_name__  # type: ignore
        description = method.__kernel_function_description__  # type: ignore
        parameters = [KernelParameterMetadata(**param) for param in method.__kernel_function_parameters__]  # type: ignore
        return_param = KernelParameterMetadata(
            name="return",
            description=method.__kernel_function_return_description__,  # type: ignore
            default_value=None,
            type=method.__kernel_function_return_type__,  # type: ignore
            is_required=method.__kernel_function_return_required__,  # type: ignore
        )

        try:
            metadata = KernelFunctionMetadata(
                name=function_name,
                description=description,
                parameters=parameters,
                return_parameter=return_param,
                is_prompt=False,
                is_asynchronous=isasyncgenfunction(method) or iscoroutinefunction(method),
                plugin_name=plugin_name,
            )
        except ValidationError as exc:
            # reraise the exception to clarify it comes from KernelFunction init
            raise FunctionInitializationError("Failed to create KernelFunctionMetadata") from exc

        args: dict[str, Any] = {
            "metadata": metadata,
            "method": method,
            "stream_method": (
                stream_method
                if stream_method is not None
                else method if isasyncgenfunction(method) or isgeneratorfunction(method) else None
            ),
        }

        super().__init__(**args)

    async def _invoke_internal(
        self,
        function_context: FunctionContext,
    ) -> None:
        """Invoke the function with the given arguments."""
        function_arguments = self.gather_function_parameters(function_context.kernel, function_context.arguments)
        result = self.method(**function_arguments)
        if isasyncgen(result):
            result = [x async for x in result]
        elif isawaitable(result):
            result = await result
        elif isgenerator(result):
            result = list(result)
        if not isinstance(result, FunctionResult):
            result = FunctionResult(
                function=self.metadata,
                value=result,
                metadata={"arguments": function_context.arguments, "used_arguments": function_arguments},
            )
        function_context.result = result

    async def _invoke_internal_stream(
        self,
        kernel: Kernel,
        arguments: KernelArguments,
    ) -> AsyncGenerator[list[StreamingContentMixin] | Any, Any]:
        if self.stream_method is None:
            raise NotImplementedError("Stream method not implemented")
        function_arguments = self.gather_function_parameters(kernel, arguments)
        if isasyncgenfunction(self.stream_method):
            async for partial_result in self.stream_method(**function_arguments):
                yield partial_result
        elif isgeneratorfunction(self.stream_method):
            for partial_result in self.stream_method(**function_arguments):
                yield partial_result

    def gather_function_parameters(self, kernel: Kernel, arguments: KernelArguments) -> dict[str, Any]:
        """Gathers the function parameters from the arguments."""
        function_arguments: dict[str, Any] = {}
        for param in self.parameters:
            if param.name == "kernel":
                function_arguments[param.name] = kernel
                continue
            if param.name == "service":
                function_arguments[param.name] = kernel.select_ai_service(self, arguments)[0]
                continue
            if param.name == "execution_settings":
                function_arguments[param.name] = kernel.select_ai_service(self, arguments)[1]
                continue
            if param.name == "arguments":
                function_arguments[param.name] = arguments
                continue
            if param.name in arguments:
                value: Any = arguments[param.name]
                if param.type_ and "," not in param.type_ and param.type_object:
                    if hasattr(param.type_object, "model_validate"):
                        try:
                            value = param.type_object.model_validate(value)
                        except Exception as exc:
                            raise FunctionExecutionException(
                                f"Parameter {param.name} is expected to be parsed to {param.type_} but is not."
                            ) from exc
                    else:
                        try:
                            value = param.type_object(value)
                        except Exception as exc:
                            raise FunctionExecutionException(
                                f"Parameter {param.name} is expected to be parsed to {param.type_} but is not."
                            ) from exc
                function_arguments[param.name] = value
                continue
            if param.is_required:
                raise FunctionExecutionException(
                    f"Parameter {param.name} is required but not provided in the arguments."
                )
            logger.debug(f"Parameter {param.name} is not provided, using default value {param.default_value}")
        return function_arguments
