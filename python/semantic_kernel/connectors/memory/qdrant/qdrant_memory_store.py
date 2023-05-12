# Copyright (c) Microsoft. All rights reserved.

"""
QdrantMemoryStore provides functionality to add Qdrant vector database to support Semantic Kernel memory.
The QdrantMemoryStore inherits from MemoryStoreBase for persisting/retrieving data from a Qdrant Vector Database.
"""
import qdrant_client
from qdrant_client import QdrantClient
from qdrant_client.http import models
from qdrant_client.http.models import Distance, VectorParams
from qdrant_client.http.models import CollectionStatus
from qdrant_client.http.models import PointStruct
from qdrant_client.http.models import UpdateStatus

from logging import Logger
from typing import List, Optional, Tuple

from numpy import array, linalg, ndarray

from semantic_kernel.memory.memory_record import MemoryRecord
from semantic_kernel.memory.memory_store_base import MemoryStoreBase
from semantic_kernel.utils.null_logger import NullLogger


class QdrantMemoryStore(MemoryStoreBase):
    _qdrantclient: qdrant_client
    _logger: Logger

    def __init__(
        self, hostip: str, port: Optional[int] = 6333, logger: Optional[Logger] = None
    ) -> None:
        self._qdrantclient = QdrantClient(host=hostip, port=port)
        self._logger = logger or NullLogger()

        """Initializes a new instance of the QdrantMemoryStore class.

        Arguments:
            logger {Optional[Logger]} -- The logger to use. (default: {None})
        """
        try:
            from qdrant_client import QdrantClient
        except ImportError:
            raise ValueError(
                "Error: Umable to import qdrant client python package."
                "Please install qdrant client using `pip install qdrant-client`."
            )

    async def create_collection_async(
        self, collection_name: str, vector_size: int, distance: Optional[str] = "Cosine"
    ) -> None:
        """Creates a new collection if it does not exist.

        Arguments:
            collection_name {str} -- The name of the collection to create.
            vector_size {int} -- The size of the vector.
            distance {Optional[str]} -- The distance metric to use. (default: {"Cosine"})
        Returns:
            None
        """

        self._qdrantclient.recreate_collection(
            collection_name="{collection_name}",
            vectors_config=VectorParams(size=vector_size, distance=Distance.COSINE),
        )

    async def get_collections_async(
        self,
    ) -> List[str]:
        """Gets the list of collections.

        Returns:
            List[str] -- The list of collections.
        """
        return list(self._qdrantclient.get_collections())

    async def delete_collection_async(self, collection_name: str) -> None:
        """Deletes a collection.

        Arguments:
            collection_name {str} -- The name of the collection to delete.

        Returns:
            None
        """

        self._qdrantclient.delete_collection(collection_name=collection_name)

    async def does_collection_exist_async(self, collection_name: str) -> bool:
        """Checks if a collection exists.

        Arguments:
            collection_name {str} -- The name of the collection to check.

        Returns:
            bool -- True if the collection exists; otherwise, False.
        """

        collection_info = self._qdrantclient.get_collection(
            collection_name=collection_name
        )

        if collection_info.status == CollectionStatus.GREEN:
            return collection_name
        else:
            return ""

    async def upsert_async(self, collection_name: str, record: MemoryRecord) -> str:
        """Upserts a record.

        Arguments:
            collection_name {str} -- The name of the collection to upsert the record into.
            record {MemoryRecord} -- The record to upsert.

        Returns:
            str -- The unqiue database key of the record.
        """
        record._key = record._id

        collection_info = self.does_collection_exist_async(
            collection_name=collection_name
        )

        if not collection_info:
            raise Exception(f"Collection '{collection_name}' does not exist")

        upsert_info = self._qdrantclient.upsert(
            collection_name=collection_name,
            wait=True,
            points=[
                PointStruct(
                    id=record._id, vector=record._embedding, payload=record._payload
                ),
            ],
        )

        if upsert_info.status == UpdateStatus.COMPLETED:
            return record._key
        else:
            return ""

    async def upsert_batch_async(
        self, collection_name: str, records: List[MemoryRecord]
    ) -> List[str]:
        """Upserts a batch of records.

        Arguments:
            collection_name {str} -- The name of the collection to upsert the records into.
            records {List[MemoryRecord]} -- The records to upsert.

        Returns:
            List[str] -- The unqiue database keys of the records.
        """

        collection_info = self.does_collection_exist_async(
            collection_name=collection_name
        )

        if not collection_info:
            raise Exception(f"Collection '{collection_name}' does not exist")

        points_rec = []

        for record in records:
            record._key = record._id
            pointstruct = PointStruct(
                id=record._id, vector=record._embedding, payload=record._payload
            )
            points_rec.append([pointstruct])
            upsert_info = self._qdrantclient.upsert(
                collection_name=collection_name, wait=True, points=points_rec
            )

        if upsert_info.status == UpdateStatus.COMPLETED:
            return [record._key for record in records]
        else:
            return ""

    async def get_async(
        self, collection_name: str, key: str, with_embedding: bool = False
    ) -> MemoryRecord:
        """Gets a record.

        Arguments:
            collection_name {str} -- The name of the collection to get the record from.
            key {str} -- The unique database key of the record.
            with_embedding {bool} -- Whether to include the embedding in the result. (default: {False})

        Returns:
            MemoryRecord -- The record.
        """

        collection_info = self._qdrantclient.get_collection(
            collection_name=collection_name
        )
        with_payload = True
        search_id = [key]

        if not collection_info.status == CollectionStatus.GREEN:
            raise Exception(f"Collection '{collection_name}' does not exist")

        qdrant_record = self._qdrantclient.retrieve(
            collection_name=collection_name,
            ids=search_id,
            with_payload=with_payload,
            with_vectors=with_embedding,
        )

        result = MemoryRecord(
            is_reference=False,
            external_source_name="qdrant",
            key=search_id,
            id=search_id,
            embedding=qdrant_record.vector,
            payload=qdrant_record.payload,
        )

        return result

    async def get_batch_async(
        self, collection_name: str, keys: List[str], with_embeddings: bool = False
    ) -> List[MemoryRecord]:
        """Gets a batch of records.

        Arguments:
            collection_name {str} -- The name of the collection to get the records from.
            keys {List[str]} -- The unique database keys of the records.
            with_embeddings {bool} -- Whether to include the embeddings in the results. (default: {False})

        Returns:
            List[MemoryRecord] -- The records.
        """

        collection_info = self._qdrantclient.get_collection(
            collection_name=collection_name
        )

        if not collection_info.status == CollectionStatus.GREEN:
            raise Exception(f"Collection '{collection_name}' does not exist")

        with_payload = True
        search_ids = [keys]

        qdrant_records = self._qdrantclient.retrieve(
            collection_name=collection_name,
            ids=search_ids,
            with_payload=with_payload,
            with_vectors=with_embeddings,
        )

        return qdrant_records

    async def remove_async(self, collection_name: str, key: str) -> None:
        """Removes a record.

        Arguments:
            collection_name {str} -- The name of the collection to remove the record from.
            key {str} -- The unique database key of the record to remove.

        Returns:
            None
        """

        collection_info = self._qdrantclient.get_collection(
            collection_name=collection_name
        )

        if not collection_info.status == CollectionStatus.GREEN:
            raise Exception(f"Collection '{collection_name}' does not exist")

        self._qdrantclient.delete(
            collection_name="{collection_name}",
            points_selector=models.PointIdsList(
                points=[key],
            ),
        )

    async def remove_batch_async(self, collection_name: str, keys: List[str]) -> None:
        """Removes a batch of records.

        Arguments:
            collection_name {str} -- The name of the collection to remove the records from.
            keys {List[str]} -- The unique database keys of the records to remove.

        Returns:
            None
        """
        collection_info = self._qdrantclient.get_collection(
            collection_name=collection_name
        )

        if not collection_info.status == CollectionStatus.GREEN:
            raise Exception(f"Collection '{collection_name}' does not exist")

        self._qdrantclient.delete(
            collection_name="{collection_name}",
            points_selector=models.PointIdsList(
                points=[keys],
            ),
        )

    async def get_nearest_match_async(
        self,
        collection_name: str,
        embedding: ndarray,
        min_relevance_score: float = 0.0,
        with_embedding: bool = False,
    ) -> Tuple[MemoryRecord, float]:
        """Gets the nearest match to an embedding using cosine similarity.

        Arguments:
            collection_name {str} -- The name of the collection to get the nearest match from.
            embedding {ndarray} -- The embedding to find the nearest match to.
            min_relevance_score {float} -- The minimum relevance score of the match. (default: {0.0})
            with_embedding {bool} -- Whether to include the embedding in the result. (default: {False})

        Returns:
            Tuple[MemoryRecord, float] -- The record and the relevance score.
        """
        return self.get_nearest_matches_async(
            collection_name=collection_name,
            embedding=embedding,
            limit=1,
            min_relevance_score=min_relevance_score,
            with_embeddings=with_embedding,
        )

    async def get_nearest_matches_async(
        self,
        collection_name: str,
        embedding: ndarray,
        limit: int,
        min_relevance_score: float = 0.0,
        with_embeddings: bool = False,
    ) -> List[Tuple[MemoryRecord, float]]:
        """Gets the nearest matches to an embedding using cosine similarity.

        Arguments:
            collection_name {str} -- The name of the collection to get the nearest matches from.
            embedding {ndarray} -- The embedding to find the nearest matches to.
            limit {int} -- The maximum number of matches to return.
            min_relevance_score {float} -- The minimum relevance score of the matches. (default: {0.0})
            with_embeddings {bool} -- Whether to include the embeddings in the results. (default: {False})

        Returns:
            List[Tuple[MemoryRecord, float]] -- The records and their relevance scores.
        """

        collection_info = self._qdrantclient.get_collection(
            collection_name=collection_name
        )

        if not collection_info.status == CollectionStatus.GREEN:
            raise Exception(f"Collection '{collection_name}' does not exist")

        # Search for the nearest matches, qdrant already provides results sorted by relevance score
        qdrant_matches = self._qdrantclient.search(
            collection_name=collection_name,
            search_params=models.SearchParams(
                hnsw_ef=0,
                exact=False,
                quantization=None,
            ),
            query_vector=embedding,
            limit=limit,
            score_threshold=min_relevance_score,
            with_vectors=with_embeddings,
            with_payload=True,
        )

        nearest_results = []

        # Convert the results to MemoryRecords
        for qdrant_match in qdrant_matches:
            vector_result = MemoryRecord(
                is_reference=False,
                external_source_name="qdrant",
                key=str(qdrant_match.id),
                id=str(qdrant_match.id),
                embedding=qdrant_match.vector,
                payload=qdrant_match.payload,
            )

            nearest_results.append(tuple(vector_result, qdrant_match.score))

        return nearest_results

    def compute_similarity_scores(
        self, embedding: ndarray, embedding_array: ndarray
    ) -> ndarray:
        """Computes the cosine similarity scores between a query embedding and a group of embeddings.

        Arguments:
            embedding {ndarray} -- The query embedding.
            embedding_array {ndarray} -- The group of embeddings.

        Returns:
            ndarray -- The cosine similarity scores.
        """
        query_norm = linalg.norm(embedding)
        collection_norm = linalg.norm(embedding_array, axis=1)

        # Compute indices for which the similarity scores can be computed
        valid_indices = (query_norm != 0) & (collection_norm != 0)

        # Initialize the similarity scores with -1 to distinguish the cases
        # between zero similarity from orthogonal vectors and invalid similarity
        similarity_scores = array([-1.0] * embedding_array.shape[0])

        if valid_indices.any():
            similarity_scores[valid_indices] = embedding.dot(
                embedding_array[valid_indices].T
            ) / (query_norm * collection_norm[valid_indices])
            if not valid_indices.all():
                self._logger.warning(
                    "Some vectors in the embedding collection are zero vectors."
                    "Ignoring cosine similarity score computation for those vectors."
                )
        else:
            raise ValueError(
                f"Invalid vectors, cannot compute cosine similarity scores"
                f"for zero vectors"
                f"{embedding_array} or {embedding}"
            )
        return similarity_scores
