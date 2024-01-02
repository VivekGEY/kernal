﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Gemini;
using Microsoft.SemanticKernel.Connectors.Gemini.Core;
using SemanticKernel.UnitTests;
using Xunit;

namespace SemanticKernel.Connectors.Gemini.UnitTests.Core;

public sealed class GeminiClientTextStreamingTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private const string TestDataFilePath = "./TestData/completion_stream_response.json";

    public GeminiClientTextStreamingTests()
    {
        this._messageHandlerStub = new HttpMessageHandlerStub();
        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(
            File.ReadAllText(TestDataFilePath));

        this._httpClient = new HttpClient(this._messageHandlerStub, false);
    }

    [Fact]
    public async Task ShouldUseUserAgentAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        _ = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        Assert.True(this._messageHandlerStub.RequestHeaders?.Contains("User-Agent"));
        IEnumerable<string> values = this._messageHandlerStub.RequestHeaders!.GetValues("User-Agent");
        string? value = values.SingleOrDefault();
        Assert.Equal("Semantic-Kernel", value);
    }

    [Fact]
    public async Task ShouldUseSpecifiedModelAsync()
    {
        // Arrange
        string modelId = "fake-model";
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: modelId);

        // Act
        _ = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        Assert.Contains(modelId, this._messageHandlerStub.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldUseSpecifiedApiKeyAsync()
    {
        // Arrange
        string fakeAPIKey = "fake-api-key";
        var client = new GeminiClient(this._httpClient, fakeAPIKey, modelId: "fake-model");

        // Act
        _ = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        Assert.Contains(fakeAPIKey, this._messageHandlerStub.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldUseBaseEndpointAsync()
    {
        // Arrange
        var baseEndPoint = GeminiEndpoints.BaseEndpoint.AbsoluteUri;
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        _ = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        Assert.StartsWith(baseEndPoint, this._messageHandlerStub.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldSendPromptToServiceAsync()
    {
        // Arrange
        string prompt = "fake-prompt";
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        _ = await client.StreamGenerateTextAsync(prompt).ToListAsync();

        // Assert
        GeminiRequest? request = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(request);
        Assert.Equal(prompt, request.Contents[0].Parts[0].Text);
    }

    [Fact]
    public async Task ShouldReturnValidModelTextResponseAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        var streamingTextContents = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        List<GeminiResponse> testDataResponse = JsonSerializer.Deserialize<List<GeminiResponse>>(
            await File.ReadAllTextAsync(TestDataFilePath))!;

        Assert.NotEmpty(streamingTextContents);
        Assert.Equal(testDataResponse.Count, streamingTextContents.Count);
        for (int i = 0; i < testDataResponse.Count; i++)
        {
            Assert.Equal(
                testDataResponse[i].Candidates[0].Content.Parts[0].Text,
                streamingTextContents[i].Text);
        }
    }

    [Fact]
    public async Task ShouldReturnValidGeminiMetadataAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        var streamingTextContents = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        List<GeminiResponse> sampleDataResponses = JsonSerializer.Deserialize<List<GeminiResponse>>(
            await File.ReadAllTextAsync(TestDataFilePath))!;
        var testDataResponse = sampleDataResponses[0];
        var textContent = streamingTextContents.FirstOrDefault();
        Assert.NotNull(textContent);
        var metadata = textContent.Metadata as GeminiMetadata;
        Assert.NotNull(metadata);
        Assert.Equal(testDataResponse.PromptFeedback!.BlockReason, metadata.PromptFeedbackBlockReason);
        Assert.Equal(testDataResponse.Candidates[0].FinishReason, metadata.FinishReason);
        Assert.Equal(testDataResponse.Candidates[0].Index, metadata.Index);
        Assert.True(metadata.ResponseSafetyRatings!.Count
                    == testDataResponse.Candidates[0].SafetyRatings.Count);
        Assert.True(metadata.PromptFeedbackSafetyRatings!.Count
                    == testDataResponse.PromptFeedback.SafetyRatings.Count);
        Assert.Equal(testDataResponse.UsageMetadata!.PromptTokenCount, metadata.PromptTokenCount);
        Assert.Equal(testDataResponse.Candidates[0].TokenCount, metadata.CurrentCandidateTokenCount);
        Assert.Equal(testDataResponse.UsageMetadata.CandidatesTokenCount, metadata.CandidatesTokenCount);
        Assert.Equal(testDataResponse.UsageMetadata.TotalTokenCount, metadata.TotalTokenCount);
    }

    [Fact]
    public async Task ShouldReturnValidDictionaryMetadataAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        var streamingTextContents = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        List<GeminiResponse> sampleDataResponses = JsonSerializer.Deserialize<List<GeminiResponse>>(
            await File.ReadAllTextAsync(TestDataFilePath))!;
        var testDataResponse = sampleDataResponses[0];
        var textContent = streamingTextContents.FirstOrDefault();
        Assert.NotNull(textContent);
        var metadata = textContent.Metadata;
        Assert.NotNull(metadata);
        Assert.Equal(testDataResponse.PromptFeedback!.BlockReason, metadata[nameof(GeminiMetadata.PromptFeedbackBlockReason)]);
        Assert.Equal(testDataResponse.Candidates[0].FinishReason, metadata[nameof(GeminiMetadata.FinishReason)]);
        Assert.Equal(testDataResponse.Candidates[0].Index, metadata[nameof(GeminiMetadata.Index)]);
        Assert.True(((IList<GeminiMetadataSafetyRating>)metadata[nameof(GeminiMetadata.ResponseSafetyRatings)]!).Count
                    == testDataResponse.Candidates[0].SafetyRatings.Count);
        Assert.True(((IList<GeminiMetadataSafetyRating>)metadata[nameof(GeminiMetadata.PromptFeedbackSafetyRatings)]!).Count
                    == testDataResponse.PromptFeedback.SafetyRatings.Count);
        Assert.Equal(testDataResponse.UsageMetadata!.PromptTokenCount, metadata[nameof(GeminiMetadata.PromptTokenCount)]);
        Assert.Equal(testDataResponse.Candidates[0].TokenCount, metadata[nameof(GeminiMetadata.CurrentCandidateTokenCount)]);
        Assert.Equal(testDataResponse.UsageMetadata.CandidatesTokenCount, metadata[nameof(GeminiMetadata.CandidatesTokenCount)]);
        Assert.Equal(testDataResponse.UsageMetadata.TotalTokenCount, metadata[nameof(GeminiMetadata.TotalTokenCount)]);
    }

    [Fact]
    public async Task ShouldReturnResponseWithModelIdAsync()
    {
        // Arrange
        string modelId = "fake-model";
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        var streamingTextContents = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        var textContent = streamingTextContents.FirstOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal(modelId, textContent.ModelId);
    }

    [Fact]
    public async Task ShouldReturnResponseWithValidInnerContentAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");

        // Act
        var streamingTextContents = await client.StreamGenerateTextAsync("fake-text").ToListAsync();

        // Assert
        string testDataResponseJson = JsonSerializer.Serialize(JsonSerializer.Deserialize<List<GeminiResponse>>(
            await File.ReadAllTextAsync(TestDataFilePath))![0].Candidates[0]);
        var textContent = streamingTextContents.FirstOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal(testDataResponseJson, JsonSerializer.Serialize(textContent.InnerContent));
    }

    [Fact]
    public async Task ShouldUsePromptExecutionSettingsAsync()
    {
        // Arrange
        var client = new GeminiClient(this._httpClient, "fake-api-key", modelId: "fake-model");
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 102,
            Temperature = 0.45,
            TopP = 0.6
        };

        // Act
        _ = await client.StreamGenerateTextAsync("fake-text", executionSettings).ToListAsync();

        // Assert
        var geminiRequest = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(geminiRequest);
        Assert.Equal(executionSettings.MaxTokens, geminiRequest.Configuration!.MaxOutputTokens);
        Assert.Equal(executionSettings.Temperature, geminiRequest.Configuration!.Temperature);
        Assert.Equal(executionSettings.TopP, geminiRequest.Configuration!.TopP);
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
