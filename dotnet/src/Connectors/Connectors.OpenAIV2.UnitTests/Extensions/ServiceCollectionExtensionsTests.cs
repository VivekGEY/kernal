﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.TextToImage;
using OpenAI;
using Xunit;

namespace SemanticKernel.Connectors.OpenAI.UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void ItCanAddTextEmbeddingGenerationService()
    {
        // Arrange
        var sut = new ServiceCollection();

        // Act
        var service = sut.AddOpenAITextEmbeddingGeneration("model", "key")
            .BuildServiceProvider()
            .GetRequiredService<ITextEmbeddingGenerationService>();

        // Assert
        Assert.Equal("model", service.Attributes[AIServiceExtensions.ModelIdKey]);
    }

    [Fact]
    public void ItCanAddTextEmbeddingGenerationServiceWithOpenAIClient()
    {
        // Arrange
        var sut = new ServiceCollection();

        // Act
        var service = sut.AddOpenAITextEmbeddingGeneration("model", new OpenAIClient("key"))
            .BuildServiceProvider()
            .GetRequiredService<ITextEmbeddingGenerationService>();

        // Assert
        Assert.Equal("model", service.Attributes[AIServiceExtensions.ModelIdKey]);
    }

    [Fact]
    public void ItCanAddImageToTextService()
    {
        // Arrange
        var sut = new ServiceCollection();

        // Act
        var service = sut.AddOpenAITextToImage("model", "key")
            .BuildServiceProvider()
            .GetRequiredService<ITextToImageService>();

        // Assert
        Assert.Equal("model", service.Attributes[AIServiceExtensions.ModelIdKey]);
    }

    [Fact]
    public void ItCanAddImageToTextServiceWithOpenAIClient()
    {
        // Arrange
        var sut = new ServiceCollection();

        // Act
        var service = sut.AddOpenAITextToImage("model", new OpenAIClient("key"))
            .BuildServiceProvider()
            .GetRequiredService<ITextToImageService>();

        // Assert
        Assert.Equal("model", service.Attributes[AIServiceExtensions.ModelIdKey]);
    }
}
