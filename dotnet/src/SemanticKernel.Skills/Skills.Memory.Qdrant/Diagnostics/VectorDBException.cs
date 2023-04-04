﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Diagnostics;

namespace Microsoft.SemanticKernel.Connectors.Memory.Qdrant.Diagnostics;

public class VectorDbException : Exception<VectorDbException.ErrorCodes>
{
    public enum ErrorCodes
    {
        UnknownError,
        UnableToDeserializeRecordPayload,
        NewGuidAlreadyExistsInCollection,
        FailedToConvertMemoryRecordToQdrantVectorRecord
    }

    public VectorDbException(string message)
        : this(ErrorCodes.UnknownError, message)
    {
    }

    public VectorDbException(ErrorCodes error, string message)
        : base(error, message)
    {
    }

    private VectorDbException()
    {
    }

    private VectorDbException(string message, System.Exception innerException) : base(message, innerException)
    {
    }
}
