# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

from pydantic import PrivateAttr

from semantic_kernel.connectors.ai.embeddings.embedding_generator_base import EmbeddingGeneratorBase
from semantic_kernel.memory.memory_query_result import MemoryQueryResult
from semantic_kernel.memory.memory_record import MemoryRecord
from semantic_kernel.memory.memory_store_base import MemoryStoreBase
from semantic_kernel.memory.semantic_text_memory_base import SemanticTextMemoryBase


class SemanticTextMemory(SemanticTextMemoryBase):
    _storage: MemoryStoreBase = PrivateAttr()
    # TODO: replace with kernel and service_selector pattern
    _embeddings_generator: EmbeddingGeneratorBase = PrivateAttr()

    def __init__(self, storage: MemoryStoreBase, embeddings_generator: EmbeddingGeneratorBase) -> None:
        """Initialize a new instance of SemanticTextMemory.

        Arguments:
            storage {MemoryStoreBase} -- The MemoryStoreBase to use for storage.
            embeddings_generator {EmbeddingGeneratorBase} -- The EmbeddingGeneratorBase
                to use for generating embeddings.

        Returns:
            None -- None.
        """
        super().__init__()
        self._storage = storage
        self._embeddings_generator = embeddings_generator

    async def save_information(
        self,
        collection: str,
        text: str,
        id: str,
        description: str | None = None,
        additional_metadata: str | None = None,
    ) -> None:
        """Save information to the memory (calls the memory store's upsert method).

        Arguments:
            collection {str} -- The collection to save the information to.
            text {str} -- The text to save.
            id {str} -- The id of the information.
            description {str | None} -- The description of the information.

        Returns:
            None -- None.
        """
        # TODO: not the best place to create collection, but will address this behavior together with .NET SK
        if not await self._storage.does_collection_exist(collection_name=collection):
            await self._storage.create_collection(collection_name=collection)

        embedding = (await self._embeddings_generator.generate_embeddings([text]))[0]
        data = MemoryRecord.local_record(
            id=id,
            text=text,
            description=description,
            additional_metadata=additional_metadata,
            embedding=embedding,
        )

        await self._storage.upsert(collection_name=collection, record=data)

    async def save_reference(
        self,
        collection: str,
        text: str,
        external_id: str,
        external_source_name: str,
        description: str | None = None,
        additional_metadata: str | None = None,
    ) -> None:
        """Save a reference to the memory (calls the memory store's upsert method).

        Arguments:
            collection {str} -- The collection to save the reference to.
            text {str} -- The text to save.
            external_id {str} -- The external id of the reference.
            external_source_name {str} -- The external source name of the reference.
            description {str | None} -- The description of the reference.

        Returns:
            None -- None.
        """
        # TODO: not the best place to create collection, but will address this behavior together with .NET SK
        if not await self._storage.does_collection_exist(collection_name=collection):
            await self._storage.create_collection(collection_name=collection)

        embedding = (await self._embeddings_generator.generate_embeddings([text]))[0]
        data = MemoryRecord.reference_record(
            external_id=external_id,
            source_name=external_source_name,
            description=description,
            additional_metadata=additional_metadata,
            embedding=embedding,
        )

        await self._storage.upsert(collection_name=collection, record=data)

    async def get(
        self,
        collection: str,
        key: str,
    ) -> MemoryQueryResult | None:
        """Get information from the memory (calls the memory store's get method).

        Arguments:
            collection {str} -- The collection to get the information from.
            key {str} -- The key of the information.

        Returns:
            MemoryQueryResult | None -- The MemoryQueryResult if found, None otherwise.
        """
        record = await self._storage.get(collection_name=collection, key=key)
        return MemoryQueryResult.from_memory_record(record, 1.0) if record else None

    async def search(
        self,
        collection: str,
        query: str,
        limit: int = 1,
        min_relevance_score: float = 0.0,
        with_embeddings: bool = False,
    ) -> list[MemoryQueryResult]:
        """Search the memory (calls the memory store's get_nearest_matches method).

        Arguments:
            collection {str} -- The collection to search in.
            query {str} -- The query to search for.
            limit {int} -- The maximum number of results to return. (default: {1})
            min_relevance_score {float} -- The minimum relevance score to return. (default: {0.0})
            with_embeddings {bool} -- Whether to return the embeddings of the results. (default: {False})

        Returns:
            list[MemoryQueryResult] -- The list of MemoryQueryResult found.
        """
        query_embedding = (await self._embeddings_generator.generate_embeddings([query]))[0]
        results = await self._storage.get_nearest_matches(
            collection_name=collection,
            embedding=query_embedding,
            limit=limit,
            min_relevance_score=min_relevance_score,
            with_embeddings=with_embeddings,
        )

        return [MemoryQueryResult.from_memory_record(r[0], r[1]) for r in results]

    async def get_collections(self) -> list[str]:
        """Get the list of collections in the memory (calls the memory store's get_collections method).

        Returns:
            list[str] -- The list of all the memory collection names.
        """
        return await self._storage.get_collections()
