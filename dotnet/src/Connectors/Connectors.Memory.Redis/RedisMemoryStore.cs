﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.Memory;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;
using StackExchange.Redis;

namespace Microsoft.SemanticKernel.Connectors.Memory.Redis;

/// <summary>
/// An implementation of <see cref="IMemoryStore"/> for Redis.
/// </summary>
/// <remarks>The embedded data is saved to the Redis server database specified in the constructor.
/// Similarity search capability is provided through the RediSearch module. Use RediSearch's "Index" to implement "Collection".
/// </remarks>
public sealed class RedisMemoryStore : IMemoryStore
{
    /// <summary>
    /// Create a new instance of semantic memory using Redis.
    /// </summary>
    /// <param name="database">The database of the redis server.</param>
    /// <param name="vectorSize">Embedding vector size</param>
    public RedisMemoryStore(IDatabase database, int vectorSize)
    {
        this._database = database;
        this._vectorSize = vectorSize;
        this._ft = database.FT();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var index in await this._ft._ListAsync().ConfigureAwait(false))
        {
            yield return ((string)index!);
        }
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (await this.DoesCollectionExistAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        FTCreateParams ftCreateParams = FTCreateParams.CreateParams().On(IndexDataType.HASH).Prefix($"{_skRedisKeyPrefix}:{collectionName}:");
        Schema schema = new Schema()
            .AddTextField("key")
            .AddTextField("metadata")
            .AddNumericField("timestamp")
            .AddVectorField("embedding", Schema.VectorField.VectorAlgo.HNSW, new Dictionary<string, object> {
                    {"TYPE", "FLOAT32"},
                    {"DIM", this._vectorSize},
                    {"DISTANCE_METRIC", "COSINE"},
                });

        await this._ft.CreateAsync(collectionName, ftCreateParams, schema).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._ft.InfoAsync(collectionName).ConfigureAwait(false);
            return true;
        }
        catch (RedisServerException ex) when (ex.Message == "Unknown Index name")
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (!await this.DoesCollectionExistAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // dd: If `true`, all documents will be deleted.
        await this._ft.DropIndexAsync(collectionName, dd: true).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryRecord?> GetAsync(string collectionName, string key, bool withEmbedding = false, CancellationToken cancellationToken = default)
    {
        return await this.InternalGetAsync(collectionName, key, withEmbedding, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var result = await this.InternalGetAsync(collectionName, key, withEmbeddings, cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                yield return result;
            }
            else
            {
                yield break;
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        record.Key = record.Metadata.Id;

        await this._database.HashSetAsync(GetRedisKey(collectionName, record.Key), new[] {
            new HashEntry("key", record.Key),
            new HashEntry("metadata", record.GetSerializedMetadata()),
            new HashEntry("embedding", MemoryMarshal.Cast<float, byte>(record.Embedding.AsReadOnlySpan()).ToArray()),
            new HashEntry("timestamp", ToTimestampLong(record.Timestamp))
        }, flags: CommandFlags.None).ConfigureAwait(false);

        return record.Key;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            yield return await this.UpsertAsync(collectionName, record, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        await this._database.KeyDeleteAsync(GetRedisKey(collectionName, key), flags: CommandFlags.None).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        await this._database.KeyDeleteAsync(keys.Select(key => GetRedisKey(collectionName, key)).ToArray(), flags: CommandFlags.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(
        string collectionName,
        Embedding<float> embedding,
        int limit,
        double minRelevanceScore = 0,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            yield break;
        }

        var query = new Query($"*=>[KNN {limit} @embedding $embedding AS vector_score]")
                    .AddParam("embedding", MemoryMarshal.Cast<float, byte>(embedding.AsReadOnlySpan()).ToArray())
                    .SetSortBy("vector_score")
                    .ReturnFields("key", "metadata", "embedding", "timestamp", "vector_score")
                    .Limit(0, limit)
                    .Dialect(2);

        var results = await this._ft.SearchAsync(collectionName, query).ConfigureAwait(false);

        foreach (var document in results.Documents)
        {
            double score = 1 - (double)document["vector_score"];
            if (score < minRelevanceScore)
            {
                yield break;
            }

            Embedding<float> convertedEmbedding = withEmbeddings && document["embedding"].HasValue
                ?
                new Embedding<float>(MemoryMarshal.Cast<byte, float>((byte[])document["embedding"]!).ToArray())
                :
                Embedding<float>.Empty;

            yield return (MemoryRecord.FromJsonMetadata(
                    json: document["metadata"]!,
                    embedding: convertedEmbedding,
                    key: document["key"],
                    timestamp: ParseTimestamp((long?)document["timestamp"])), score);
        }
    }

    /// <inheritdoc/>
    public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(string collectionName, Embedding<float> embedding, double minRelevanceScore = 0, bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        return await this.GetNearestMatchesAsync(
            collectionName: collectionName,
            embedding: embedding,
            limit: 1,
            minRelevanceScore: minRelevanceScore,
            withEmbeddings: withEmbedding,
            cancellationToken: cancellationToken).FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    #region private ================================================================================
    private const string _skRedisKeyPrefix = "sk-memory";
    private readonly IDatabase _database;
    private readonly int _vectorSize;
    private readonly SearchCommands _ft;

    private static long ToTimestampLong(DateTimeOffset? timestamp)
    {
        if (timestamp.HasValue)
        {
            return timestamp.Value.ToUnixTimeMilliseconds();
        }
        return -1;
    }

    private static DateTimeOffset? ParseTimestamp(long? timestamp)
    {
        if (timestamp.HasValue && timestamp > 0)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value);
        }

        return null;
    }

    private static RedisKey GetRedisKey(string collectionName, string key)
    {
        return new RedisKey($"{_skRedisKeyPrefix}:{collectionName}:{key}");
    }

    private async Task<MemoryRecord?> InternalGetAsync(string collectionName, string key, bool withEmbedding, CancellationToken cancellationToken)
    {
        HashEntry[] hashEntries = await this._database.HashGetAllAsync(GetRedisKey(collectionName, key), flags: CommandFlags.None).ConfigureAwait(false);

        if (hashEntries.Length == 0) { return null; }

        if (withEmbedding)
        {
            RedisValue embedding = hashEntries.FirstOrDefault(x => x.Name == "embedding").Value;
            return MemoryRecord.FromJsonMetadata(
                json: hashEntries.FirstOrDefault(x => x.Name == "metadata").Value!,
                embedding: embedding.HasValue ? new Embedding<float>(MemoryMarshal.Cast<byte, float>((byte[])embedding!).ToArray()) : Embedding<float>.Empty,
                key: hashEntries.FirstOrDefault(x => x.Name == "key").Value,
                timestamp: ParseTimestamp((long?)hashEntries.FirstOrDefault(x => x.Name == "timestamp").Value));
        }

        return MemoryRecord.FromJsonMetadata(
            json: hashEntries.FirstOrDefault(x => x.Name == "metadata").Value!,
            embedding: Embedding<float>.Empty,
            key: hashEntries.FirstOrDefault(x => x.Name == "key").Value,
            timestamp: ParseTimestamp((long?)hashEntries.FirstOrDefault(x => x.Name == "timestamp").Value));
    }

    #endregion
}
