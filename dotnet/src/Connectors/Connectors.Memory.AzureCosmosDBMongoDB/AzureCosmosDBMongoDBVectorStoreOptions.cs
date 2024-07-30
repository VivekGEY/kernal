﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Connectors.AzureCosmosDBMongoDB;

/// <summary>
/// Options when creating a <see cref="AzureCosmosDBMongoDBVectorStore"/>
/// </summary>
public sealed class AzureCosmosDBMongoDBVectorStoreOptions
{
    /// <summary>
    /// An optional factory to use for constructing <see cref="AzureCosmosDBMongoDBVectorStoreRecordCollection{TRecord}"/> instances, if custom options are required.
    /// </summary>
    public IAzureCosmosDBMongoDBVectorStoreRecordCollectionFactory? VectorStoreCollectionFactory { get; init; }
}