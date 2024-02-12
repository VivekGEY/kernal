﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Moq;
using Xunit;

namespace SemanticKernel.Connectors.UnitTests.Memory.AzureAISearch;

/// <summary>
/// Unit tests for <see cref="AzureAISearchMemoryStore"/> class.
/// </summary>
public sealed class AzureAISearchMemoryStoreTests
{
    private readonly Mock<SearchIndexClient> _mockSearchIndexClient = new();
    private readonly Mock<SearchClient> _mockSearchClient = new();

    private readonly AzureAISearchMemoryStore _service;

    public AzureAISearchMemoryStoreTests()
    {
        this._mockSearchIndexClient
            .Setup(x => x.GetSearchClient(It.IsAny<string>()))
            .Returns(this._mockSearchClient.Object);

        this._service = new AzureAISearchMemoryStore(this._mockSearchIndexClient.Object);
    }

    [Fact]
    public async Task GetCollectionsReturnsIndexNamesAsync()
    {
        // Arrange
        Page<SearchIndex> page = Page<SearchIndex>.FromValues(new[]
        {
            new SearchIndex("index-1"),
            new SearchIndex("index-2"),
        }, null, Mock.Of<Response>());

        var pageable = AsyncPageable<SearchIndex>.FromPages([page]);

        this._mockSearchIndexClient
            .Setup(x => x.GetIndexesAsync(It.IsAny<CancellationToken>()))
            .Returns(pageable);

        // Act
        var indexes = new List<string>();

        await foreach (var index in this._service.GetCollectionsAsync())
        {
            indexes.Add(index);
        }

        // Assert
        Assert.Equal("index-1", indexes[0]);
        Assert.Equal("index-2", indexes[1]);
    }

    [Fact]
    public async Task GetCollectionsOnErrorThrowsHttpOperationExceptionAsync()
    {
        // Arrange
        this._mockSearchIndexClient
            .Setup(x => x.GetIndexesAsync(It.IsAny<CancellationToken>()))
            .Throws(new RequestFailedException((int)HttpStatusCode.InternalServerError, "test error response"));

        // Act & Assert
        var indexes = new List<string>();

        var exception = await Assert.ThrowsAsync<HttpOperationException>(async () =>
        {
            await foreach (var index in this._service.GetCollectionsAsync())
            {
            }
        });

        Assert.Equal("test error response", exception.Message);
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Theory]
    [InlineData("index-1", true)]
    [InlineData("index-2", true)]
    [InlineData("index-3", false)]
    public async Task DoesCollectionExistReturnsValidResultAsync(string collectionName, bool expectedResult)
    {
        // Arrange
        Page<SearchIndex> page = Page<SearchIndex>.FromValues(new[]
        {
            new SearchIndex("index-1"),
            new SearchIndex("index-2"),
        }, null, Mock.Of<Response>());

        var pageable = AsyncPageable<SearchIndex>.FromPages([page]);

        this._mockSearchIndexClient
            .Setup(x => x.GetIndexesAsync(It.IsAny<CancellationToken>()))
            .Returns(pageable);

        // Act
        var result = await this._service.DoesCollectionExistAsync(collectionName);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task DoesCollectionExistOnErrorThrowsHttpOperationExceptionAsync()
    {
        // Arrange
        this._mockSearchIndexClient
            .Setup(x => x.GetIndexesAsync(It.IsAny<CancellationToken>()))
            .Throws(new RequestFailedException((int)HttpStatusCode.InternalServerError, "test error response"));

        // Act
        var exception = await Assert.ThrowsAsync<HttpOperationException>(() => this._service.DoesCollectionExistAsync("test-index"));

        // Assert
        Assert.Equal("test error response", exception.Message);
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task DeleteCollectionWorksCorrectlyAsync()
    {
        // Arrange
        this._mockSearchIndexClient
            .Setup(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Response>());

        // Act
        await this._service.DeleteCollectionAsync("test-index");

        // Assert
        this._mockSearchIndexClient.Verify(x => x.DeleteIndexAsync("test-index", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteCollectionOnErrorThrowsHttpOperationExceptionAsync()
    {
        // Arrange
        this._mockSearchIndexClient
            .Setup(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new RequestFailedException((int)HttpStatusCode.InternalServerError, "test error response"));

        // Act
        var exception = await Assert.ThrowsAsync<HttpOperationException>(() => this._service.DeleteCollectionAsync("test-index"));

        // Assert
        Assert.Equal("test error response", exception.Message);
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task GetReturnsValidRecordAsync()
    {
        // Arrange
        this._mockSearchClient
            .Setup(x => x.GetDocumentAsync<AzureAISearchMemoryRecord>(It.IsAny<string>(), It.IsAny<GetDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new AzureAISearchMemoryRecord("record-id"), Mock.Of<Response>()));

        // Act
        var result = await this._service.GetAsync("test-index", "record-id");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(AzureAISearchMemoryRecord.EncodeId("record-id"), result.Key);
    }

    [Fact]
    public async Task GetReturnsNullWhenRecordDoesNotExistAsync()
    {
        // Arrange
        this._mockSearchClient
            .Setup(x => x.GetDocumentAsync<AzureAISearchMemoryRecord>(It.IsAny<string>(), It.IsAny<GetDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "test error response"));

        // Act
        var result = await this._service.GetAsync("test-collection", "test-record");

        // Assert
        Assert.Null(result);
    }
}
