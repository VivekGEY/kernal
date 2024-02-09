# Copyright (c) Microsoft. All rights reserved.

from abc import ABC, abstractmethod
from typing import TYPE_CHECKING, List

from semantic_kernel.kernel_pydantic import KernelBaseModel

if TYPE_CHECKING:
    from semantic_kernel.functions.kernel_arguments import KernelArguments
    from semantic_kernel.functions.kernel_parameter_metadata import KernelParameterMetadata
    from semantic_kernel.kernel import Kernel


class PromptTemplateBase(KernelBaseModel, ABC):
    @abstractmethod
    def get_parameters(self) -> List["KernelParameterMetadata"]:
        pass

    @abstractmethod
    async def render(self, kernel: "Kernel", arguments: "KernelArguments") -> str:
        pass
