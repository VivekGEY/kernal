# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

import logging
from inspect import isasyncgen, isasyncgenfunction, isawaitable, iscoroutinefunction, isgenerator, isgeneratorfunction
from typing import TYPE_CHECKING, Any, Callable

from pydantic import ValidationError

from semantic_kernel.exceptions import FunctionExecutionException, FunctionInitializationError
from semantic_kernel.filters.function.function_invocation_context import FunctionInvocationContext
from semantic_kernel.functions.function_result import FunctionResult
from semantic_kernel.functions.kernel_function import KernelFunction
from semantic_kernel.functions.kernel_function_metadata import KernelFunctionMetadata
from semantic_kernel.functions.kernel_parameter_metadata import KernelParameterMetadata

if TYPE_CHECKING:
    pass


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
        parameters: list[KernelParameterMetadata] | None = None,
        return_parameter: KernelParameterMetadata | None = None,
        additional_metadata: dict[str, Any] | None = None,
    ) -> None:
        """
        Initializes a new instance of the KernelFunctionFromMethod class

        Args:
            method (Callable[..., Any]): The method to be called
            plugin_name (str | None): The name of the plugin
            stream_method (Callable[..., Any] | None): The stream method for the function
            parameters (list[KernelParameterMetadata] | None): The parameters of the function
            return_parameter (KernelParameterMetadata | None): The return parameter of the function
            additional_metadata (dict[str, Any] | None): Additional metadata for the function
        """
        if method is None:
            raise FunctionInitializationError("Method cannot be `None`")

        if not hasattr(method, "__kernel_function__") or method.__kernel_function__ is None:
            raise FunctionInitializationError("Method is not a Kernel function")

        # all these fields are created when the kernel function decorator is used,
        # so no need to check before using, will raise an exception if not set
        function_name = method.__kernel_function_name__  # type: ignore
        description = method.__kernel_function_description__  # type: ignore
        if parameters is None:
            parameters = [KernelParameterMetadata(**param) for param in method.__kernel_function_parameters__]  # type: ignore
        if return_parameter is None:
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
                additional_properties=additional_metadata if additional_metadata is not None else {},
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
        context: FunctionInvocationContext,
    ) -> None:
        """Invoke the function with the given arguments."""
        function_arguments = self.gather_function_parameters(context)
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
                metadata={"arguments": context.arguments, "used_arguments": function_arguments},
            )
        context.result = result

    async def _invoke_internal_stream(self, context: FunctionInvocationContext) -> None:
        if self.stream_method is None:
            raise NotImplementedError("Stream method not implemented")
        function_arguments = self.gather_function_parameters(context)
        context.result = FunctionResult(function=self.metadata, value=self.stream_method(**function_arguments))

    def gather_function_parameters(self, context: FunctionInvocationContext) -> dict[str, Any]:
        """Gathers the function parameters from the arguments."""
        function_arguments: dict[str, Any] = {}
        for param in self.parameters:
            if param.name == "kernel":
                function_arguments[param.name] = context.kernel
                continue
            if param.name == "service":
                function_arguments[param.name] = context.kernel.select_ai_service(self, context.arguments)[0]
                continue
            if param.name == "execution_settings":
                function_arguments[param.name] = context.kernel.select_ai_service(self, context.arguments)[1]
                continue
            if param.name == "arguments":
                function_arguments[param.name] = context.arguments
                continue
            if param.name in context.arguments:
                value: Any = context.arguments[param.name]
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
