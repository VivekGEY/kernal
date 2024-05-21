# Copyright (c) Microsoft. All rights reserved.

from abc import ABC, abstractmethod
from typing import TYPE_CHECKING, Any

from semantic_kernel.services.ai_service_client_base import AIServiceClientBase
from semantic_kernel.utils.experimental_decorator import experimental_class

if TYPE_CHECKING:
    from numpy import ndarray


@experimental_class
class EmbeddingGeneratorBase(AIServiceClientBase, ABC):
    @abstractmethod
    async def generate_embeddings(self, texts: list[str], **kwargs: Any) -> "ndarray":
        pass
