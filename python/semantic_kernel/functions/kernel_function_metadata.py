# Copyright (c) Microsoft. All rights reserved.

import sys
from typing import List

if sys.version_info >= (3, 9):
    pass
else:
    pass

from pydantic import Field

from semantic_kernel.functions.kernel_parameter_metadata import KernelParameterMetadata
from semantic_kernel.kernel_pydantic import KernelBaseModel
from semantic_kernel.utils.validation import FUNCTION_NAME_REGEX


class KernelFunctionMetadata(KernelBaseModel):
    name: str = Field(pattern=FUNCTION_NAME_REGEX)
    plugin_name: str
    description: str
    parameters: List[KernelParameterMetadata] = Field(default_factory=list)
    is_semantic: bool
    is_asynchronous: bool = True

    def __eq__(self, other: "KernelFunctionMetadata") -> bool:
        """
        Compare to another KernelFunctionMetadata instance.

        Args:
            other (KernelFunctionMetadata): The other KernelFunctionMetadata instance.

        Returns:
            True if the two instances are equal, False otherwise.
        """
        if not isinstance(other, KernelFunctionMetadata):
            return False

        return (
            self.name == other.name
            and self.plugin_name == other.plugin_name
            and self.description == other.description
            and self.parameters == other.parameters
            and self.is_semantic == other.is_semantic
            and self.is_asynchronous == other.is_asynchronous
        )
