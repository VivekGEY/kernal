# Copyright (c) Microsoft. All rights reserved.

from typing import TYPE_CHECKING, Optional, Protocol, runtime_checkable

if TYPE_CHECKING:
    from semantic_kernel import Kernel
    from semantic_kernel.functions.kernel_arguments import KernelArguments


@runtime_checkable
class TextRenderer(Protocol):
    """Protocol for static (text) blocks that do not need async rendering."""

    def render(self, kernel: "Kernel", arguments: Optional["KernelArguments"] = None) -> str:
        """Render the block using only the given variables.

        :param variables: Optional variables used to render the block
        :return: Rendered content
        """
