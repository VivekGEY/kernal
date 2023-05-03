﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Model;
using Microsoft.SemanticKernel.Memory;

namespace Microsoft.SemanticKernel.Connectors.Memory.Pinecone;

internal enum OperationType
{
    Upsert,
    Update,
    Skip
}

/// <summary>
/// An implementation of <see cref="IMemoryStore"/> for Pinecone Vector database.
/// </summary>
/// <remarks>
/// The Embedding data is saved to a Pinecone Vector database instance that the client is connected to.
/// The embedding data persists between subsequent instances and has similarity search capability.
/// It should be noted that "Collection" in Pinecone's terminology is much different than what Collection means in IMemoryStore.
/// For that reason, we use the term "Index" in Pinecone to refer to what is a "Collection" in IMemoryStore. So, in the case of Pinecone,
///  "Collection" is synonymous with "Index" when referring to IMemoryStore.
/// </remarks>
public class PineconeMemoryStore : IPineconeMemoryStore
{

    /// <summary>
    /// Constructor for a memory store backed by a <see cref="IPineconeClient"/>
    /// </summary>
    public PineconeMemoryStore(
        IPineconeClient pineconeClient,
        ILogger? logger = null)
    {
        this._pineconeClient = pineconeClient;
        this._logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    ///  Constructor for a Pinecone memory store when an index already exists.
    /// </summary>
    /// <param name="pineconeEnvironment"> the location of the pinecone server </param>
    /// <param name="apiKey"> the user's api key for the pinecone server </param>
    /// <param name="logger"></param>
    public PineconeMemoryStore(
        PineconeEnvironment pineconeEnvironment,
        string apiKey,
        ILogger? logger = null)
    {
        this._logger = logger ?? NullLogger.Instance;
        this._pineconeClient = new PineconeClient(pineconeEnvironment, apiKey, logger);
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="PineconeMemoryStore"/> class and ensures that the index exists and is ready.
    /// </summary>
    /// <param name="pineconeEnvironment"> the location of the pinecone server </param>
    /// <param name="apiKey"> the api key for the pinecone server </param>
    /// <param name="indexDefinition"> the index definition </param>
    /// <param name="logger"></param>
    /// <param name="cancellationToken"></param>
    /// <returns> a new instance of the <see cref="PineconeMemoryStore"/> class </returns>
    /// <remarks>
    ///  If the index does not exist, it will be created. If the index exists, it will be connected to.
    ///  If it is a new index, the method will block until it is ready.
    ///  If the index exists but is not ready, it will be connected to and the method will block until it is ready.
    /// </remarks>
    public static async Task<PineconeMemoryStore?> InitializeAsync(
        PineconeEnvironment pineconeEnvironment,
        string apiKey,
        IndexDefinition indexDefinition,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {

        logger ??= NullLogger.Instance;
        PineconeClient? client = new(pineconeEnvironment, apiKey, logger);
        PineconeMemoryStore? store = null;

        try
        {
            bool exists = await client.DoesIndexExistAsync(indexDefinition.Name, cancellationToken).ConfigureAwait(false);

            if (exists)
            {
                if (await client.ConnectToHostAsync(indexDefinition.Name, cancellationToken).ConfigureAwait(true))
                {
                    store = new PineconeMemoryStore(client, logger);
                    client = null; // Ownership transferred to store, so set to null
                }

                else
                {
                    logger.LogError("Failed to connect to host.");
                }

                return store;
            }

            string? indexName = await client.CreateIndexAsync(indexDefinition, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(indexName) && client.Ready)
            {
                store = new PineconeMemoryStore(client, logger);
                client = null; // Ownership transferred to store, so set to null
            }
            else
            {
                logger.LogError("Failed to create index.");
            }

            return store;
        }

        finally
        {
            // Only dispose the client if we did not create a store.
            client?.Dispose();
        }

    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="cancellationToken"></param> 
    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (!await this.DoesCollectionExistAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            IndexDefinition indexDefinition = IndexDefinition.Create(collectionName);
            await this._pineconeClient.CreateIndexAsync(indexDefinition, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <returns> a list of index names </returns>
    public IAsyncEnumerable<string> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        return this._pineconeClient.ListIndexesAsync(cancellationToken).Select(index => index ?? "");
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="cancellationToken"></param>
    public async Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return await this._pineconeClient.DoesIndexExistAsync(collectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="cancellationToken"></param>
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (await this.DoesCollectionExistAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            await this._pineconeClient.DeleteIndexAsync(collectionName, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        return await this.UpsertToNamespaceAsync(collectionName, string.Empty, record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> UpsertToNamespaceAsync(string indexName, string indexNamespace, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return string.Empty;
        }

        (PineconeDocument vectorData, OperationType operationType) = await this.EvaluateAndUpdateMemoryRecordAsync(indexName, record, indexNamespace, cancellationToken).ConfigureAwait(false);

        Task request = operationType switch
        {
            OperationType.Upsert => this._pineconeClient.UpsertAsync(indexName, new[] { vectorData }, indexNamespace, cancellationToken),
            OperationType.Update => this._pineconeClient.UpdateAsync(indexName, vectorData, indexNamespace, cancellationToken),
            OperationType.Skip => Task.CompletedTask,
            _ => Task.CompletedTask
        };

        try
        {
            await request.ConfigureAwait(false);

        }
        catch (HttpRequestException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToUpsertVectors,
                $"Failed to upsert due to HttpRequestException: {ex.Message}",
                ex);
        }

        return vectorData.Id;
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="records"></param>
    /// <param name="cancellationToken"></param>
    public async IAsyncEnumerable<string> UpsertBatchAsync(
        string collectionName,
        IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (string id in this.UpsertBatchToNamespaceAsync(collectionName, string.Empty, records, cancellationToken).ConfigureAwait(false))
        {
            yield return id;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> UpsertBatchToNamespaceAsync(
        string indexName,
        string indexNamespace,
        IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            yield break;
        }

        List<PineconeDocument> upsertDocuments = new();
        List<PineconeDocument> updateDocuments = new();

        foreach (MemoryRecord? record in records)
        {
            (PineconeDocument document, OperationType operationType) = await this.EvaluateAndUpdateMemoryRecordAsync(
                indexName,
                record,
                indexNamespace,
                cancellationToken).ConfigureAwait(false);

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (operationType)
            {
                case OperationType.Upsert:
                    upsertDocuments.Add(document);
                    break;

                case OperationType.Update:

                    updateDocuments.Add(document);
                    break;

                case OperationType.Skip:
                    yield return document.Id;
                    break;

            }
        }

        List<Task> tasks = new();

        if (upsertDocuments.Count > 0)
        {
            tasks.Add(this._pineconeClient.UpsertAsync(indexName, upsertDocuments, indexNamespace, cancellationToken));
        }

        if (updateDocuments.Count > 0)
        {
            IEnumerable<Task> updates = updateDocuments.Select(async d
                => await this._pineconeClient.UpdateAsync(indexName, d, indexNamespace, cancellationToken).ConfigureAwait(false));

            tasks.AddRange(updates);
        }

        PineconeDocument[] vectorData = upsertDocuments.Concat(updateDocuments).ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToUpsertVectors,
                $"Failed to upsert due to HttpRequestException: {ex.Message}",
                ex);
        }

        foreach (PineconeDocument? v in vectorData)
        {
            yield return v.Id;
        }
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="key"></param>
    /// <param name="withEmbedding"></param>
    /// <param name="cancellationToken"></param>
    public async Task<MemoryRecord?> GetAsync(
        string collectionName,
        string key,
        bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        return await this.GetFromNamespaceAsync(collectionName, string.Empty, key, withEmbedding, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryRecord?> GetFromNamespaceAsync(
        string indexName,
        string indexNamespace,
        string key,
        bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return null;
        }

        try
        {
            await foreach (PineconeDocument? record in this._pineconeClient.FetchVectorsAsync(indexName,
                new[] { key },
                indexNamespace,
                withEmbedding, cancellationToken))
            {
                return record?.ToMemoryRecord();
            }
        }
        catch (HttpRequestException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToGetVectorData,
                $"Failed to get vector data from Pinecone: {ex.Message}",
                ex);
        }
        catch (MemoryException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToConvertPineconeDocumentToMemoryRecord,
                $"Failed deserialize Pinecone response to Memory Record: {ex.Message}",
                ex);
        }

        return null;
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="keys"></param>
    /// <param name="withEmbeddings"></param>
    /// <param name="cancellationToken"></param>
    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(
        string collectionName,
        IEnumerable<string> keys,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {

        await foreach (MemoryRecord? record in this.GetBatchFromNamespaceAsync(collectionName, string.Empty, keys, withEmbeddings, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryRecord> GetBatchFromNamespaceAsync(
        string indexName,
        string indexNamespace,
        IEnumerable<string> keys,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            yield break;
        }

        foreach (string? key in keys)
        {
            MemoryRecord? record = await this.GetFromNamespaceAsync(indexName, indexNamespace, key, withEmbeddings, cancellationToken).ConfigureAwait(false);

            if (record != null)
            {
                yield return record;
            }
        }
    }

    /// <summary>
    /// Get a MemoryRecord from the Pinecone Vector database by pointId.
    /// </summary>
    /// <param name="indexName">The name associated with the index to get the Pinecone vector record from.</param>
    /// <param name="documentId">The unique indexed ID associated with the Pinecone vector record to get.</param>
    /// <param name="limit"></param>
    /// <param name="indexNamespace"> The namespace associated with the Pinecone vector record to get.</param>
    /// <param name="withEmbedding">If true, the embedding will be returned in the memory record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="PineconeMemoryException"></exception>
    public async IAsyncEnumerable<MemoryRecord?> GetWithDocumentIdAsync(string indexName,
        string documentId,
        int limit = 3,
        string indexNamespace = "",
        bool withEmbedding = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (MemoryRecord? record in this.GetWithDocumentIdBatchAsync(indexName, new[] { documentId }, limit, indexNamespace, withEmbedding, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    /// <summary>
    /// Get a MemoryRecord from the Pinecone Vector database by a group of pointIds.
    /// </summary>
    /// <param name="indexName">The name associated with the index to get the Pinecone vector records from.</param>
    /// <param name="documentIds">The unique indexed IDs associated with Pinecone vector records to get.</param>
    /// <param name="limit"></param>
    /// <param name="indexNamespace"> The namespace associated with the Pinecone vector records to get.</param>
    /// <param name="withEmbeddings">If true, the embeddings will be returned in the memory records.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async IAsyncEnumerable<MemoryRecord?> GetWithDocumentIdBatchAsync(string indexName,
        IEnumerable<string> documentIds,
        int limit = 3,
        string indexNamespace = "",
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            yield break;
        }

        foreach (IAsyncEnumerable<MemoryRecord?>? records in documentIds.Select(documentId =>
            this.GetWithDocumentIdAsync(indexName, documentId, limit, indexNamespace, withEmbeddings, cancellationToken)))
        {
            await foreach (MemoryRecord? record in records.WithCancellation(cancellationToken))
            {
                yield return record;
            }
        }
    }

    public async IAsyncEnumerable<MemoryRecord?> GetBatchWithFilterAsync(string indexName,
        Dictionary<string, object> filter,
        int limit = 10,
        string indexNamespace = "",
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            yield break;
        }

        IEnumerable<PineconeDocument?> vectorDataList;

        try
        {
            Query query = Query.Create(limit)
                .InNamespace(indexNamespace)
                .WithFilter(filter);

            vectorDataList = await this._pineconeClient
                .QueryAsync(indexName,
                    query,
                    cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        catch (HttpRequestException e)
        {
            this._logger.LogError(e, "Error getting batch with filter from Pinecone.");
            yield break;
        }

        foreach (PineconeDocument? record in vectorDataList)
        {
            yield return record?.ToMemoryRecord();
        }
    }

    /// <inheritdoc />
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    public async Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        await this.RemoveFromNamespaceAsync(collectionName, string.Empty, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveFromNamespaceAsync(string indexName, string indexNamespace, string key, CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return;
        }

        try
        {
            await this._pineconeClient.DeleteAsync(indexName, new[]
                {
                    key
                },
                indexNamespace,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToRemoveVectorData,
                $"Failed to remove vector data from Pinecone {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="keys"></param>
    /// <param name="cancellationToken"></param>
    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        await this.RemoveBatchFromNamespaceAsync(collectionName, string.Empty, keys, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveBatchFromNamespaceAsync(string indexName, string indexNamespace, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return;
        }
        await Task.WhenAll(keys.Select(async k => await this.RemoveFromNamespaceAsync(indexName, indexNamespace, k, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveWithFilterAsync(
        string indexName,
        Dictionary<string, object> filter,
        string indexNamespace = "",
        CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return;
        }

        try
        {
            await this._pineconeClient.DeleteAsync(
                indexName,
                default,
                indexNamespace,
                filter,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToRemoveVectorData,
                $"Failed to remove vector data from Pinecone {ex.Message}",
                ex);
        }

    }

    /// <summary>
    /// Remove a MemoryRecord from the Pinecone Vector database by pointId.
    /// </summary>
    /// <param name="indexName"> The name associated with the index to remove the Pinecone vector record from.</param>
    /// <param name="indexNamespace">The name associated with a collection of embeddings.</param>
    /// <param name="documentId">The unique indexed ID associated with the Pinecone vector record to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="PineconeMemoryException"></exception>
    public async Task RemoveWithDocumentIdAsync(string indexName, string documentId, string indexNamespace, CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return;
        }

        try
        {
            await this._pineconeClient.DeleteAsync(indexName, null, indexNamespace, new Dictionary<string, object>()
            {
                { "document_Id", documentId }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToRemoveVectorData,
                $"Failed to remove vector data from Pinecone {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Remove a MemoryRecord from the Pinecone Vector database by a group of pointIds.
    /// </summary>
    /// <param name="indexName"> The name associated with the index to remove the Pinecone vector record from.</param>
    /// <param name="indexNamespace">The name associated with a collection of embeddings.</param>
    /// <param name="documentIds">The unique indexed IDs associated with the Pinecone vector records to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="PineconeMemoryException"></exception>
    public async Task RemoveWithDocumentIdBatchAsync(
        string indexName,
        IEnumerable<string> documentIds,
        string indexNamespace,
        CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return;
        }

        try
        {
            IEnumerable<Task> tasks = documentIds.Select(async id
                => await this.RemoveWithDocumentIdAsync(indexName, id, indexNamespace, cancellationToken)
                    .ConfigureAwait(false));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new PineconeMemoryException(
                PineconeMemoryException.ErrorCodes.FailedToRemoveVectorData,
                $"Error in batch removing data from Pinecone {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="embedding"> The embedding to search for </param>
    /// <param name="limit"> The maximum number of results to return </param>
    /// <param name="minRelevanceScore"> The minimum relevance score to return </param>
    /// <param name="withEmbeddings"> Whether to return the embeddings with the results </param>
    /// <param name="cancellationToken"></param>
    public IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(
        string collectionName,
        Embedding<float> embedding,
        int limit,
        double minRelevanceScore = 0,
        bool withEmbeddings = false,
        CancellationToken cancellationToken = default)
    {
        return this.GetNearestMatchesFromNamespaceAsync(
            collectionName,
            string.Empty,
            embedding,
            limit,
            minRelevanceScore,
            withEmbeddings,
            cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesFromNamespaceAsync(
        string indexName,
        string indexNamespace,
        Embedding<float> embedding,
        int limit,
        double minRelevanceScore = 0,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            yield break;
        }

        IAsyncEnumerable<(PineconeDocument, double)> results = this._pineconeClient.GetMostRelevantAsync(
            indexName,
            embedding.Vector,
            minRelevanceScore,
            limit,
            withEmbeddings,
            true,
            indexNamespace,
            default,
            cancellationToken);

        await foreach ((PineconeDocument, double) result in results.WithCancellation(cancellationToken))
        {
            yield return (result.Item1.ToMemoryRecord(), result.Item2);
        }
    }

    /// <inheritdoc/>
    /// <param name="collectionName"> in the case of Pinecone, collectionName is synonymous with indexName </param>
    /// <param name="embedding"> The embedding to search for </param>
    /// <param name="minRelevanceScore"> The minimum relevance score to return </param>
    /// <param name="withEmbedding"> Whether to return the embeddings with the results </param>
    /// <param name="cancellationToken"></param>
    public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(
        string collectionName,
        Embedding<float> embedding,
        double minRelevanceScore = 0,
        bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        return await this.GetNearestMatchFromNamespaceAsync(
            collectionName,
            string.Empty,
            embedding,
            minRelevanceScore,
            withEmbedding,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(MemoryRecord, double)?> GetNearestMatchFromNamespaceAsync(
        string indexName,
        string indexNamespace,
        Embedding<float> embedding,
        double minRelevanceScore = 0,
        bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            return null;
        }

        IAsyncEnumerable<(MemoryRecord, double)> results = this.GetNearestMatchesFromNamespaceAsync(
            indexName,
            indexNamespace,
            embedding,
            minRelevanceScore: minRelevanceScore,
            limit: 1,
            withEmbeddings: withEmbedding,
            cancellationToken: cancellationToken);

        (MemoryRecord, double) record = await results.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return (record.Item1, record.Item2);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesWithFilterAsync(
        string indexName,
        Embedding<float> embedding,
        int limit,
        Dictionary<string, object> filter,
        double minRelevanceScore = 0D,
        string indexNamespace = "",
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await this.EnsurePineconeClientReadyAsync(indexName, cancellationToken).ConfigureAwait(false))
        {
            this._logger.LogError("Pinecone client is not ready.");
            yield break;
        }

        IAsyncEnumerable<(PineconeDocument, double)> results = this._pineconeClient.GetMostRelevantAsync(
            indexName,
            embedding.Vector,
            minRelevanceScore,
            limit,
            withEmbeddings,
            true,
            indexNamespace,
            filter,
            cancellationToken);

        await foreach ((PineconeDocument, double) result in results.WithCancellation(cancellationToken))
        {
            yield return (result.Item1.ToMemoryRecord(), result.Item2);
        }
    }

    /// <inheritdoc />
    public async Task ClearNamespaceAsync(string indexName, string indexNamespace, CancellationToken cancellationToken = default)
    {
        await this._pineconeClient.DeleteAsync(indexName, default, indexNamespace, null, true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string?> ListNamespacesAsync(string indexName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IndexStats? indexStats = await this._pineconeClient.DescribeIndexStatsAsync(indexName, default, cancellationToken).ConfigureAwait(false);

        if (indexStats is null)
        {
            yield break;
        }

        foreach (string? indexNamespace in indexStats.Namespaces.Keys)
        {
            yield return indexNamespace;
        }
    }

    #region private ================================================================================

    private readonly IPineconeClient _pineconeClient;
    private readonly ILogger _logger;
    
    // a async method that ensures the pinecone client is ready
    private async Task<bool> EnsurePineconeClientReadyAsync(string indexName, CancellationToken cancellationToken = default)
    {
        if (this._pineconeClient.Ready)
        {
            return true;
        }
        
        PineconeClient client = (PineconeClient)this._pineconeClient;

        return await client.ConnectToHostAsync(indexName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(PineconeDocument, OperationType)> EvaluateAndUpdateMemoryRecordAsync(
        string indexName,
        MemoryRecord record,
        string indexNamespace = "",
        CancellationToken cancel = default)
    {
        string key = !string.IsNullOrEmpty(record.Key)
            ? record.Key
            : record.Metadata.Id;

        PineconeDocument vectorData = record.ToPineconeDocument();

        PineconeDocument? existingRecord = await this._pineconeClient.FetchVectorsAsync(indexName, new[] { key }, indexNamespace, false, cancel)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);

        if (existingRecord is null)
        {
            return (vectorData, OperationType.Upsert);
        }

        // compare metadata dictionaries
        if (existingRecord.Metadata != null && vectorData.Metadata != null)
        {
            if (existingRecord.Metadata.SequenceEqual(vectorData.Metadata))
            {
                return (vectorData, OperationType.Skip);
            }
        }

        return (vectorData, OperationType.Update);
    }

    #endregion

}
