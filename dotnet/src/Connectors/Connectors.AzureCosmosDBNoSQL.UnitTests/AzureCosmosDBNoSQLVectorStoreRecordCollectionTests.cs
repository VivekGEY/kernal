﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.SemanticKernel.Data;
using Moq;
using Xunit;

namespace SemanticKernel.Connectors.AzureCosmosDBNoSQL.UnitTests;

/// <summary>
/// Unit tests for <see cref="AzureCosmosDBNoSQLVectorStoreRecordCollection{TRecord}"/> class.
/// </summary>
public sealed class AzureCosmosDBNoSQLVectorStoreRecordCollectionTests
{
    private readonly Mock<Database> _mockDatabase = new();
    private readonly Mock<Container> _mockContainer = new();

    public AzureCosmosDBNoSQLVectorStoreRecordCollectionTests()
    {
        this._mockDatabase
            .Setup(l => l.GetContainer(It.IsAny<string>()))
            .Returns(this._mockContainer.Object);
    }

    [Fact]
    public void ConstructorForModelWithoutKeyThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new AzureCosmosDBNoSQLVectorStoreRecordCollection<object>(this._mockDatabase.Object, "collection"));
        Assert.Contains("No key property found", exception.Message);
    }

    [Fact]
    public void ConstructorWithDeclarativeModelInitializesCollection()
    {
        // Act & Assert
        var collection = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        Assert.NotNull(collection);
    }

    [Fact]
    public void ConstructorWithImperativeModelInitializesCollection()
    {
        // Arrange
        var definition = new VectorStoreRecordDefinition
        {
            Properties = [new VectorStoreRecordKeyProperty("Id", typeof(string))]
        };

        // Act
        var collection = new AzureCosmosDBNoSQLVectorStoreRecordCollection<TestModel>(
            this._mockDatabase.Object,
            "collection",
            new() { VectorStoreRecordDefinition = definition });

        // Assert
        Assert.NotNull(collection);
    }

    [Theory]
    [MemberData(nameof(CollectionExistsData))]
    public async Task CollectionExistsReturnsValidResultAsync(List<string> collections, string collectionName, bool expectedResult)
    {
        // Arrange
        var mockFeedResponse = new Mock<FeedResponse<string>>();
        mockFeedResponse
            .Setup(l => l.Resource)
            .Returns(collections);

        var mockFeedIterator = new Mock<FeedIterator<string>>();
        mockFeedIterator
            .SetupSequence(l => l.HasMoreResults)
            .Returns(true)
            .Returns(false);

        mockFeedIterator
            .Setup(l => l.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFeedResponse.Object);

        this._mockDatabase
            .Setup(l => l.GetContainerQueryIterator<string>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockFeedIterator.Object);

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            collectionName);

        // Act
        var actualResult = await sut.CollectionExistsAsync();

        // Assert
        Assert.Equal(expectedResult, actualResult);
    }

    [Fact]
    public async Task CreateCollectionUsesValidContainerPropertiesAsync()
    {
        // Arrange
        const string CollectionName = "collection";

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            CollectionName);

        var expectedVectorEmbeddingPolicy = new VectorEmbeddingPolicy([new Embedding
        {
            DataType = VectorDataType.Float32,
            Dimensions = 4,
            DistanceFunction = Microsoft.Azure.Cosmos.DistanceFunction.Cosine,
            Path = "/description_embedding"
        }]);

        var expectedIndexingPolicy = new IndexingPolicy
        {
            VectorIndexes = [new VectorIndexPath { Type = VectorIndexType.Flat, Path = "/description_embedding" }]
        };

        var expectedContainerProperties = new ContainerProperties(CollectionName, "/id")
        {
            VectorEmbeddingPolicy = expectedVectorEmbeddingPolicy,
            IndexingPolicy = expectedIndexingPolicy
        };

        // Act
        await sut.CreateCollectionAsync();

        // Assert
        this._mockDatabase.Verify(l => l.CreateContainerAsync(
            It.Is<ContainerProperties>(properties => this.VerifyContainerProperties(expectedContainerProperties, properties)),
            It.IsAny<int?>(),
            It.IsAny<RequestOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Theory]
    [MemberData(nameof(CreateCollectionIfNotExistsData))]
    public async Task CreateCollectionIfNotExistsInvokesValidMethodsAsync(List<string> collections, int actualCollectionCreations)
    {
        // Arrange
        const string CollectionName = "collection";

        var mockFeedResponse = new Mock<FeedResponse<string>>();
        mockFeedResponse
            .Setup(l => l.Resource)
            .Returns(collections);

        var mockFeedIterator = new Mock<FeedIterator<string>>();
        mockFeedIterator
            .SetupSequence(l => l.HasMoreResults)
            .Returns(true)
            .Returns(false);

        mockFeedIterator
            .Setup(l => l.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFeedResponse.Object);

        this._mockDatabase
            .Setup(l => l.GetContainerQueryIterator<string>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockFeedIterator.Object);

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            CollectionName);

        // Act
        await sut.CreateCollectionIfNotExistsAsync();

        // Assert
        this._mockDatabase.Verify(l => l.CreateContainerAsync(
            It.IsAny<ContainerProperties>(),
            It.IsAny<int?>(),
            It.IsAny<RequestOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Exactly(actualCollectionCreations));
    }

    [Fact]
    public async Task DeleteInvokesValidMethodsAsync()
    {
        // Arrange
        const string RecordKey = "key";

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        // Act
        await sut.DeleteAsync(RecordKey);

        // Assert
        this._mockContainer.Verify(l => l.DeleteItemAsync<JsonObject>(
            RecordKey,
            new PartitionKey(RecordKey),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task DeleteBatchInvokesValidMethodsAsync()
    {
        // Arrange
        List<string> recordKeys = ["key1", "key2"];

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        // Act
        await sut.DeleteBatchAsync(recordKeys);

        // Assert
        foreach (var key in recordKeys)
        {
            this._mockContainer.Verify(l => l.DeleteItemAsync<JsonObject>(
                key,
                new PartitionKey(key),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
                Times.Once());
        }
    }

    [Fact]
    public async Task DeleteCollectionInvokesValidMethodsAsync()
    {
        // Arrange
        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        // Act
        await sut.DeleteCollectionAsync();

        // Assert
        this._mockContainer.Verify(l => l.DeleteContainerAsync(
            It.IsAny<ContainerRequestOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task GetReturnsValidRecordAsync()
    {
        // Arrange
        const string RecordKey = "key";

        var jsonObject = new JsonObject { ["id"] = RecordKey, ["HotelName"] = "Test Name" };

        var mockFeedResponse = new Mock<FeedResponse<JsonObject>>();
        mockFeedResponse
            .Setup(l => l.Resource)
            .Returns([jsonObject]);

        var mockFeedIterator = new Mock<FeedIterator<JsonObject>>();
        mockFeedIterator
            .SetupSequence(l => l.HasMoreResults)
            .Returns(true)
            .Returns(false);

        mockFeedIterator
            .Setup(l => l.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFeedResponse.Object);

        this._mockContainer
            .Setup(l => l.GetItemQueryIterator<JsonObject>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockFeedIterator.Object);

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        // Act
        var result = await sut.GetAsync(RecordKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RecordKey, result.HotelId);
        Assert.Equal("Test Name", result.HotelName);
    }

    [Fact]
    public async Task GetBatchReturnsValidRecordAsync()
    {
        // Arrange
        var jsonObject1 = new JsonObject { ["id"] = "key1", ["HotelName"] = "Test Name 1" };
        var jsonObject2 = new JsonObject { ["id"] = "key2", ["HotelName"] = "Test Name 2" };
        var jsonObject3 = new JsonObject { ["id"] = "key3", ["HotelName"] = "Test Name 3" };

        var mockFeedResponse = new Mock<FeedResponse<JsonObject>>();
        mockFeedResponse
            .Setup(l => l.Resource)
            .Returns([jsonObject1, jsonObject2, jsonObject3]);

        var mockFeedIterator = new Mock<FeedIterator<JsonObject>>();
        mockFeedIterator
            .SetupSequence(l => l.HasMoreResults)
            .Returns(true)
            .Returns(false);

        mockFeedIterator
            .Setup(l => l.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFeedResponse.Object);

        this._mockContainer
            .Setup(l => l.GetItemQueryIterator<JsonObject>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockFeedIterator.Object);

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        // Act
        var results = await sut.GetBatchAsync(["key1", "key2", "key3"]).ToListAsync();

        // Assert
        Assert.NotNull(results[0]);
        Assert.Equal("key1", results[0].HotelId);
        Assert.Equal("Test Name 1", results[0].HotelName);

        Assert.NotNull(results[1]);
        Assert.Equal("key2", results[1].HotelId);
        Assert.Equal("Test Name 2", results[1].HotelName);

        Assert.NotNull(results[2]);
        Assert.Equal("key3", results[2].HotelId);
        Assert.Equal("Test Name 3", results[2].HotelName);
    }

    [Fact]
    public async Task UpsertReturnsRecordKeyAsync()
    {
        // Arrange
        var hotel = new AzureCosmosDBNoSQLHotel("key") { HotelName = "Test Name" };

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        // Act
        var result = await sut.UpsertAsync(hotel);

        // Assert
        Assert.Equal("key", result);

        this._mockContainer.Verify(l => l.UpsertItemAsync<JsonNode>(
            It.Is<JsonNode>(node =>
                node["id"]!.ToString() == "key" &&
                node["HotelName"]!.ToString() == "Test Name"),
            new PartitionKey("key"),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task UpsertBatchReturnsRecordKeysAsync()
    {
        // Arrange
        var hotel1 = new AzureCosmosDBNoSQLHotel("key1") { HotelName = "Test Name 1" };
        var hotel2 = new AzureCosmosDBNoSQLHotel("key2") { HotelName = "Test Name 2" };
        var hotel3 = new AzureCosmosDBNoSQLHotel("key3") { HotelName = "Test Name 3" };

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection");

        // Act
        var results = await sut.UpsertBatchAsync([hotel1, hotel2, hotel3]).ToListAsync();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        Assert.Equal("key1", results[0]);
        Assert.Equal("key2", results[1]);
        Assert.Equal("key3", results[2]);
    }

    [Fact]
    public async Task UpsertWithCustomMapperWorksCorrectlyAsync()
    {
        // Arrange
        var hotel = new AzureCosmosDBNoSQLHotel("key") { HotelName = "Test Name" };

        var mockMapper = new Mock<IVectorStoreRecordMapper<AzureCosmosDBNoSQLHotel, JsonObject>>();

        mockMapper
            .Setup(l => l.MapFromDataToStorageModel(It.IsAny<AzureCosmosDBNoSQLHotel>()))
            .Returns(new JsonObject { ["id"] = "key", ["my_name"] = "Test Name" });

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection",
            new() { JsonObjectCustomMapper = mockMapper.Object });

        // Act
        var result = await sut.UpsertAsync(hotel);

        // Assert
        Assert.Equal("key", result);

        this._mockContainer.Verify(l => l.UpsertItemAsync<JsonNode>(
            It.Is<JsonNode>(node =>
                node["id"]!.ToString() == "key" &&
                node["my_name"]!.ToString() == "Test Name"),
            new PartitionKey("key"),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task GetWithCustomMapperWorksCorrectlyAsync()
    {
        // Arrange
        const string RecordKey = "key";

        var jsonObject = new JsonObject { ["id"] = RecordKey, ["HotelName"] = "Test Name" };

        var mockFeedResponse = new Mock<FeedResponse<JsonObject>>();
        mockFeedResponse
            .Setup(l => l.Resource)
            .Returns([jsonObject]);

        var mockFeedIterator = new Mock<FeedIterator<JsonObject>>();
        mockFeedIterator
            .SetupSequence(l => l.HasMoreResults)
            .Returns(true)
            .Returns(false);

        mockFeedIterator
            .Setup(l => l.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFeedResponse.Object);

        this._mockContainer
            .Setup(l => l.GetItemQueryIterator<JsonObject>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockFeedIterator.Object);

        var mockMapper = new Mock<IVectorStoreRecordMapper<AzureCosmosDBNoSQLHotel, JsonObject>>();

        mockMapper
            .Setup(l => l.MapFromStorageToDataModel(It.IsAny<JsonObject>(), It.IsAny<StorageToDataModelMapperOptions>()))
            .Returns(new AzureCosmosDBNoSQLHotel(RecordKey) { HotelName = "Name from mapper" });

        var sut = new AzureCosmosDBNoSQLVectorStoreRecordCollection<AzureCosmosDBNoSQLHotel>(
            this._mockDatabase.Object,
            "collection",
            new() { JsonObjectCustomMapper = mockMapper.Object });

        // Act
        var result = await sut.GetAsync(RecordKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RecordKey, result.HotelId);
        Assert.Equal("Name from mapper", result.HotelName);
    }

    public static TheoryData<List<string>, string, bool> CollectionExistsData => new()
    {
        { ["collection-2"], "collection-2", true },
        { [], "non-existent-collection", false }
    };

    public static TheoryData<List<string>, int> CreateCollectionIfNotExistsData => new()
    {
        { ["collection"], 0 },
        { [], 1 }
    };

    #region

    private bool VerifyContainerProperties(ContainerProperties expected, ContainerProperties actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.PartitionKeyPath, actual.PartitionKeyPath);

        for (var i = 0; i < expected.VectorEmbeddingPolicy.Embeddings.Count; i++)
        {
            var expectedEmbedding = expected.VectorEmbeddingPolicy.Embeddings[i];
            var actualEmbedding = actual.VectorEmbeddingPolicy.Embeddings[i];

            Assert.Equal(expectedEmbedding.DataType, actualEmbedding.DataType);
            Assert.Equal(expectedEmbedding.Dimensions, actualEmbedding.Dimensions);
            Assert.Equal(expectedEmbedding.DistanceFunction, actualEmbedding.DistanceFunction);
            Assert.Equal(expectedEmbedding.Path, actualEmbedding.Path);
        }

        for (var i = 0; i < expected.IndexingPolicy.VectorIndexes.Count; i++)
        {
            var expectedIndexPath = expected.IndexingPolicy.VectorIndexes[i];
            var actualIndexPath = actual.IndexingPolicy.VectorIndexes[i];

            Assert.Equal(expectedIndexPath.Type, actualIndexPath.Type);
            Assert.Equal(expectedIndexPath.Path, actualIndexPath.Path);
        }

        return true;
    }

    private sealed class TestModel
    {
        public string? Id { get; set; }

        public string? HotelName { get; set; }
    }

    #endregion
}
