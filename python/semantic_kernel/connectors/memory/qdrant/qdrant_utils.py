# Copyright (c) Microsoft. All rights reserved.

import datetime
import uuid
import struct

from qdrant_client import QdrantClient
from qdrant_client.models import Filter, FieldCondition, MatchValue

from numpy import zeros

from semantic_kernel.memory.memory_record import MemoryRecord

from typing import Optional

"""
Utility function(s) for Qdrant vector database to support Qdrant Semantic Kernel memory implementation.
"""


def guid_comb_generator() -> str:
    """
    Generate a GUID-comb identifier.

    Returns:
        str: A GUID-comb identifier.
    """

    guid_bytearr = bytearray(str(uuid.uuid4()), "utf-8")

    # Get the current time in 100-nanosecond intervals since 1/1/1
    # Convert the time to a byte array
    currenttime = datetime.datetime.utcnow()
    daystickssec = (currenttime - datetime.datetime(1970, 1, 1)).total_seconds() * 1000
    currtickssec = currenttime.microsecond / 1000

    daysbytes = struct.pack(">i", int(daystickssec))
    currbytes = struct.pack(">i", int(currtickssec))

    daysbytearr = bytearray(daysbytes)
    currbytearr = bytearray(currbytes)

    daysbytearr.reverse()
    currbytearr.reverse()

    return str(guid_bytearr)


def convert_from_memory_record(
    qdrant_client: QdrantClient,
    collection_name: str,
    record: MemoryRecord,
    vector_size: Optional[int] = 0,
) -> dict:
    """Converts a memory record to a Qdrant vector record.
       Added to be in parity with C# Semantic Kernel implementation.

    Arguments:
        qdrant_client {QdrantClient} -- The connected Qdrant client.
        collection_name {str} -- The name of the collection.
        record {MemoryRecord} -- The memory record.

    Returns:
        Tuple[string, List[ScoredPoint]] --  Converted record to Tuple of
        str: Collection name and ScoredQdrant vector record.
    """
    point_id = str()

    if record.key:
        point_id = record.key
    else:
        if vector_size == 0:
            collection_info = qdrant_client.get_collection(
                collection_name=collection_name
            )

        vector_size = collection_info.config.params.vectors.size
        search_vector = zeros(vector_size)

        vector_data = qdrant_client.search(
            collection_name=collection_name,
            query_vector=search_vector,
            query_filter=Filter(
                must=[
                    FieldCondition(
                        key="id",
                        match=MatchValue(value=record.metadata.id),
                    )
                ]
            ),
            limit=1,
        )

        if vector_data:
            point_id = str(vector_data[0].id)
        else:
            point_id = uuid.uuid4()
            id = [str(point_id)]
            vector_data = qdrant_client.retrieve(
                collection_name=collection_name,
                ids=id,
            )
            while vector_data:
                point_id = uuid.uuid4()
                id = [str(point_id)]
                vector_data = qdrant_client.retrieve(
                    collection_name=collection_name,
                    ids=id,
                )

    result = dict(
        collectionname=collection_name,
        pointid=point_id,
        vector=record.embedding,
        payload=record.payload,
    )

    return result
