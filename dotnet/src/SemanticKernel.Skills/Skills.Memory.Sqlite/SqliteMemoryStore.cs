﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.AI.Embeddings.VectorOperations;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Memory.Collections;
using Microsoft.SemanticKernel.Memory.Storage;

namespace Microsoft.SemanticKernel.Skills.Memory.Sqlite;
public class SqliteMemoryStore<TEmbedding> : IMemoryStore<TEmbedding>, IDisposable
    where TEmbedding : unmanaged
{
    public SqliteMemoryStore(SqliteConnection dbConnection)
    {
        this._dbConnection = dbConnection;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<(IEmbeddingWithMetadata<TEmbedding>, double)> GetNearestMatchesAsync(
        string collection,
        Embedding<TEmbedding> embedding,
        int limit = 1,
        double minRelevanceScore = 0)
    {
        if (limit <= 0)
        {
            return AsyncEnumerable.Empty<(IEmbeddingWithMetadata<TEmbedding>, double)>();
        }

        IAsyncEnumerable<DataEntry<IEmbeddingWithMetadata<TEmbedding>>> asyncEmbeddingCollection = this.TryGetCollectionAsync(collection);
        IEnumerable<DataEntry<IEmbeddingWithMetadata<TEmbedding>>> embeddingCollection = asyncEmbeddingCollection.ToEnumerable();

        if (embeddingCollection == null || !embeddingCollection!.Any())
        {
            return AsyncEnumerable.Empty<(IEmbeddingWithMetadata<TEmbedding>, double)>();
        }

        EmbeddingReadOnlySpan<TEmbedding> embeddingSpan = new(embedding.AsReadOnlySpan());

        TopNCollection<IEmbeddingWithMetadata<TEmbedding>> embeddings = new(limit);

        foreach (var item in embeddingCollection!)
        {
            if (item.Value != null)
            {
                EmbeddingReadOnlySpan<TEmbedding> itemSpan = new(item.Value.Embedding.AsReadOnlySpan());
                double similarity = embeddingSpan.CosineSimilarity(itemSpan);
                if (similarity >= minRelevanceScore)
                {
                    embeddings.Add(new(item.Value, similarity));
                }
            }
        }

        embeddings.SortByScore();

        return embeddings.Select(x => (x.Value, x.Score.Value)).ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancel = default)
    {
        await this._dbConnection.OpenAsync(cancel);

        await foreach (var elem in this._dbConnection.GetCollectionsAsync(cancel))
        {
            yield return elem;
        }

        await this._dbConnection.CloseAsync();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DataEntry<IEmbeddingWithMetadata<TEmbedding>>> GetAllAsync(string collection,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        await this._dbConnection.OpenAsync(cancel);

        await foreach (DatabaseEntry dbEntry in this._dbConnection.ReadAllAsync(collection, cancel))
        {
            var embedding = new Embedding<float>();
            var val = (IEmbeddingWithMetadata<TEmbedding>)MemoryRecord.FromJson(dbEntry.Value, embedding);
            yield return DataEntry.Create<IEmbeddingWithMetadata<TEmbedding>>(dbEntry.Key, val, ParseTimestamp(dbEntry.Timestamp));
        }

        await this._dbConnection.CloseAsync();
    }

    /// <inheritdoc/>
    public async Task<DataEntry<IEmbeddingWithMetadata<TEmbedding>>?> GetAsync(string collection, string key, CancellationToken cancel = default)
    {
        await this._dbConnection.OpenAsync(cancel);

        DatabaseEntry? entry = await this._dbConnection.ReadAsync(collection, key, cancel);

        await this._dbConnection.CloseAsync();

        if (entry.HasValue)
        {
            DatabaseEntry dbEntry = entry.Value;
            var embedding = new Embedding<float>();
            var val = (IEmbeddingWithMetadata<TEmbedding>)MemoryRecord.FromJson(dbEntry.Value, embedding);

            return DataEntry.Create<IEmbeddingWithMetadata<TEmbedding>>(dbEntry.Key, val, ParseTimestamp(dbEntry.Timestamp));
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<DataEntry<IEmbeddingWithMetadata<TEmbedding>>> PutAsync(string collection, DataEntry<IEmbeddingWithMetadata<TEmbedding>> data, CancellationToken cancel = default)
    {
        await this._dbConnection.OpenAsync(cancel);

        await this._dbConnection.UpsertAsync(collection, data.Key, JsonSerializer.Serialize(data.Value), ToTimestampString(data.Timestamp), cancel);

        await this._dbConnection.CloseAsync();
        return data;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string collection, string key, CancellationToken cancel = default)
    {
        await this._dbConnection.OpenAsync(cancel);

        await this._dbConnection.DeleteAsync(collection, key, cancel);

        await this._dbConnection.CloseAsync();
        return;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #region protected ================================================================================

    /// <summary>
    /// Performs dispose specifically on the SqliteConnection
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposedValue)
        {
            if (disposing)
            {
                this._dbConnection.Dispose();
            }

            this._disposedValue = true;
        }
    }

    /// <summary>
    /// Get all entries in the database that match the collectionName; Limited to the datatable initially supplied when openning the SqliteConnection.
    /// </summary>
    protected async IAsyncEnumerable<DataEntry<IEmbeddingWithMetadata<TEmbedding>>> TryGetCollectionAsync(string collectionName, [EnumeratorCancellation] CancellationToken cancel = default)
    {
        await this._dbConnection.OpenAsync(cancel);

        await foreach (DatabaseEntry dbEntry in this._dbConnection.ReadAllAsync(collectionName, cancel))
        {
            var embedding = new Embedding<float>();
            var val = (IEmbeddingWithMetadata<TEmbedding>)MemoryRecord.FromJson(dbEntry.Value, new Embedding<float>());

            yield return DataEntry.Create<IEmbeddingWithMetadata<TEmbedding>>(dbEntry.Key, val, ParseTimestamp(dbEntry.Timestamp));
        }

        await this._dbConnection.CloseAsync();
    }

    #endregion

    #region private ================================================================================

    private readonly SqliteConnection _dbConnection;
    private bool _disposedValue;

    /// <summary>
    /// Convert string to timestamp
    /// </summary>
    private static string? ToTimestampString(DateTimeOffset? timestamp)
    {
        return timestamp?.ToString("u", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Convert timestamp to string
    /// </summary>
    private static DateTimeOffset? ParseTimestamp(string? str)
    {
        if (!string.IsNullOrEmpty(str)
            && DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset timestamp))
        {
            return timestamp;
        }

        return null;
    }

    /// <summary>
    /// Calculates the cosine similarity between an <see cref="Embedding{TEmbedding}"/> and an <see cref="IEmbeddingWithMetadata{TEmbedding}"/>
    /// </summary>
    /// <param name="embedding">The input <see cref="Embedding{TEmbedding}"/> to be compared.</param>
    /// <param name="embeddingWithData">The input <see cref="IEmbeddingWithMetadata{TEmbedding}"/> to be compared.</param>
    /// <returns>A tuple consisting of the <see cref="IEmbeddingWithMetadata{TEmbedding}"/> cosine similarity result.</returns>
    private (IEmbeddingWithMetadata<TEmbedding>, double) PairEmbeddingWithSimilarity(Embedding<TEmbedding> embedding,
        IEmbeddingWithMetadata<TEmbedding> embeddingWithData)
    {
        var similarity = embedding.Vector.ToArray().CosineSimilarity(embeddingWithData.Embedding.Vector.ToArray());
        return (embeddingWithData, similarity);
    }

    #endregion
}
