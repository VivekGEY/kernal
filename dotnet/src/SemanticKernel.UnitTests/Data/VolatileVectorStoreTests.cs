﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Data;
using Xunit;

namespace SemanticKernel.UnitTests.Data;

/// <summary>
/// Contains tests for the <see cref="VolatileVectorStore"/> class.
/// </summary>
public class VolatileVectorStoreTests
{
    private const string TestCollectionName = "testcollection";

    [Fact]
    public void GetCollectionReturnsCollection()
    {
        // Arrange.
        var sut = new VolatileVectorStore();

        // Act.
        var actual = sut.GetCollection<string, SinglePropsModel<string>>(TestCollectionName);

        // Assert.
        Assert.NotNull(actual);
        Assert.IsType<VolatileVectorStoreRecordCollection<SinglePropsModel<string>>>(actual);
    }

    [Fact]
    public void GetCollectionThrowsForInvalidKeyType()
    {
        // Arrange.
        var sut = new VolatileVectorStore();

        // Act & Assert.
        Assert.Throws<NotSupportedException>(() => sut.GetCollection<int, SinglePropsModel<int>>(TestCollectionName));
    }

    [Fact]
    public async Task ListCollectionNamesReadsDictionaryAsync()
    {
        // Arrange.
        var collectionStore = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();
        collectionStore.TryAdd("collection1", new ConcurrentDictionary<string, object>());
        collectionStore.TryAdd("collection2", new ConcurrentDictionary<string, object>());
        var sut = new VolatileVectorStore(collectionStore);

        // Act.
        var collectionNames = sut.ListCollectionNamesAsync();

        // Assert.
        var collectionNamesList = await collectionNames.ToListAsync();
        Assert.Equal(new[] { "collection1", "collection2" }, collectionNamesList);
    }

    public sealed class SinglePropsModel<TKey>
    {
        [VectorStoreRecordKey]
        public required TKey Key { get; set; }

        [VectorStoreRecordData]
        public string Data { get; set; } = string.Empty;

        [VectorStoreRecordVector(4)]
        public ReadOnlyMemory<float>? Vector { get; set; }

        public string? NotAnnotated { get; set; }
    }
}
