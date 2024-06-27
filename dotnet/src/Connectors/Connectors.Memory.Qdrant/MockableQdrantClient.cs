﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Microsoft.SemanticKernel.Connectors.Qdrant;

/// <summary>
/// Decorator class for <see cref="QdrantClient"/> that exposes the required methods as virtual allowing for mocking in unit tests.
/// </summary>
internal class MockableQdrantClient
{
    /// <summary>Qdrant client that can be used to manage the collections and points in a Qdrant store.</summary>
    private readonly QdrantClient _qdrantClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockableQdrantClient"/> class.
    /// </summary>
    /// <param name="qdrantClient">Qdrant client that can be used to manage the collections and points in a Qdrant store.</param>
    public MockableQdrantClient(QdrantClient qdrantClient)
    {
        Verify.NotNull(qdrantClient);
        this._qdrantClient = qdrantClient;
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    /// <summary>
    /// Constructor for mocking purposes only.
    /// </summary>
    internal MockableQdrantClient()
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Delete a point.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="id">The ID to delete.</param>
    /// <param name="wait">Whether to wait until the changes have been applied. Defaults to <c>true</c>.</param>
	/// <param name="ordering">Write ordering guarantees. Defaults to <c>Weak</c>.</param>
	/// <param name="shardKeySelector">Option for custom sharding to specify used shard keys.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public virtual Task<UpdateResult> DeleteAsync(
        string collectionName,
        ulong id,
        bool wait = true,
        WriteOrderingType? ordering = null,
        ShardKeySelector? shardKeySelector = null,
        CancellationToken cancellationToken = default)
        => this._qdrantClient.DeleteAsync(collectionName, id, wait, ordering, shardKeySelector, cancellationToken: cancellationToken);

    /// <summary>
    /// Delete a point.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="id">The ID to delete.</param>
    /// <param name="wait">Whether to wait until the changes have been applied. Defaults to <c>true</c>.</param>
	/// <param name="ordering">Write ordering guarantees. Defaults to <c>Weak</c>.</param>
	/// <param name="shardKeySelector">Option for custom sharding to specify used shard keys.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public virtual Task<UpdateResult> DeleteAsync(
        string collectionName,
        Guid id,
        bool wait = true,
        WriteOrderingType? ordering = null,
        ShardKeySelector? shardKeySelector = null,
        CancellationToken cancellationToken = default)
        => this._qdrantClient.DeleteAsync(collectionName, id, wait, ordering, shardKeySelector, cancellationToken: cancellationToken);

    /// <summary>
    /// Delete a point.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="ids">The IDs to delete.</param>
    /// <param name="wait">Whether to wait until the changes have been applied. Defaults to <c>true</c>.</param>
	/// <param name="ordering">Write ordering guarantees. Defaults to <c>Weak</c>.</param>
	/// <param name="shardKeySelector">Option for custom sharding to specify used shard keys.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public virtual Task<UpdateResult> DeleteAsync(
        string collectionName,
        IReadOnlyList<ulong> ids,
        bool wait = true,
        WriteOrderingType? ordering = null,
        ShardKeySelector? shardKeySelector = null,
        CancellationToken cancellationToken = default)
        => this._qdrantClient.DeleteAsync(collectionName, ids, wait, ordering, shardKeySelector, cancellationToken: cancellationToken);

    /// <summary>
    /// Delete a point.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="ids">The IDs to delete.</param>
    /// <param name="wait">Whether to wait until the changes have been applied. Defaults to <c>true</c>.</param>
	/// <param name="ordering">Write ordering guarantees. Defaults to <c>Weak</c>.</param>
	/// <param name="shardKeySelector">Option for custom sharding to specify used shard keys.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public virtual Task<UpdateResult> DeleteAsync(
        string collectionName,
        IReadOnlyList<Guid> ids,
        bool wait = true,
        WriteOrderingType? ordering = null,
        ShardKeySelector? shardKeySelector = null,
        CancellationToken cancellationToken = default)
        => this._qdrantClient.DeleteAsync(collectionName, ids, wait, ordering, shardKeySelector, cancellationToken: cancellationToken);

    /// <summary>
    /// Perform insert and updates on points. If a point with a given ID already exists, it will be overwritten.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="points">The points to be upserted.</param>
    /// <param name="wait">Whether to wait until the changes have been applied. Defaults to <c>true</c>.</param>
    /// <param name="ordering">Write ordering guarantees.</param>
    /// <param name="shardKeySelector">Option for custom sharding to specify used shard keys.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public virtual Task<UpdateResult> UpsertAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        bool wait = true,
        WriteOrderingType? ordering = null,
        ShardKeySelector? shardKeySelector = null,
        CancellationToken cancellationToken = default)
        => this._qdrantClient.UpsertAsync(collectionName, points, wait, ordering, shardKeySelector, cancellationToken);

    /// <summary>
    /// Retrieve points.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="ids">List of points to retrieve.</param>
    /// <param name="withPayload">Whether to include the payload or not.</param>
    /// <param name="withVectors">Whether to include the vectors or not.</param>
    /// <param name="readConsistency">Options for specifying read consistency guarantees.</param>
    /// <param name="shardKeySelector">Option for custom sharding to specify used shard keys.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None" />.
    /// </param>
    public virtual Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(
        string collectionName,
        IReadOnlyList<PointId> ids,
        bool withPayload = true,
        bool withVectors = false,
        ReadConsistency? readConsistency = null,
        ShardKeySelector? shardKeySelector = null,
        CancellationToken cancellationToken = default)
        => this._qdrantClient.RetrieveAsync(collectionName, ids, withPayload, withVectors, readConsistency, shardKeySelector, cancellationToken);
}
