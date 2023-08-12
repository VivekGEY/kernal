﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Memory.Chroma.Http.ApiSchema;

namespace Microsoft.SemanticKernel.Connectors.Memory.Chroma;

/// <summary>
/// Interface for client to make requests to Chroma API.
/// </summary>
public interface IChromaClient
{
    /// <summary>
    /// Creates Chroma collection.
    /// </summary>
    /// <param name="collectionName">Collection name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns collection model instance by name.
    /// </summary>
    /// <param name="collectionName">Collection name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Instance of <see cref="ChromaCollectionModel"/> model.</returns>
    Task<ChromaCollectionModel?> GetCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes collection by name.
    /// </summary>
    /// <param name="collectionName">Collection name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all collection names.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>An asynchronous list of collection names.</returns>
    IAsyncEnumerable<string> ListCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts embedding to specified collection.
    /// </summary>
    /// <param name="collectionId">Collection identifier generated by Chroma.</param>
    /// <param name="ids">Array of embedding identifiers.</param>
    /// <param name="embeddings">Array of embedding vectors.</param>
    /// <param name="metadatas">Array of embedding metadatas.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    Task UpsertEmbeddingsAsync(string collectionId, string[] ids, ReadOnlyMemory<float>[] embeddings, object[]? metadatas = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns embeddings from specified collection.
    /// </summary>
    /// <param name="collectionId">Collection identifier generated by Chroma.</param>
    /// <param name="ids">Array of embedding identifiers.</param>
    /// <param name="include">Array of entities to include in response (e.g. "embeddings", "metadatas" "documents"). For more information see: https://github.com/chroma-core/chroma/blob/main/chromadb/api/types.py</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Instance of <see cref="ChromaEmbeddingsModel"/> model.</returns>
    Task<ChromaEmbeddingsModel> GetEmbeddingsAsync(string collectionId, string[] ids, string[]? include = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes embeddings from specified collection.
    /// </summary>
    /// <param name="collectionId">Collection identifier generated by Chroma.</param>
    /// <param name="ids">Array of embedding identifiers.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    Task DeleteEmbeddingsAsync(string collectionId, string[] ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches nearest embeddings by distance in specified collection.
    /// </summary>
    /// <param name="collectionId">Collection identifier generated by Chroma.</param>
    /// <param name="queryEmbeddings">Embeddings to search for.</param>
    /// <param name="nResults">Number of results to return.</param>
    /// <param name="include">Array of entities to include in response (e.g. "embeddings", "metadatas" "documents"). For more information see: https://github.com/chroma-core/chroma/blob/main/chromadb/api/types.py</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Instance of <see cref="ChromaQueryResultModel"/> model.</returns>
    Task<ChromaQueryResultModel> QueryEmbeddingsAsync(string collectionId, ReadOnlyMemory<float>[] queryEmbeddings, int nResults, string[]? include = null, CancellationToken cancellationToken = default);
}
