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
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Gemini.Core;
using Microsoft.SemanticKernel.Connectors.Gemini.Settings;
using SemanticKernel.UnitTests;
using Xunit;

namespace SemanticKernel.Connectors.Gemini.UnitTests.Core;

public sealed class GeminiClientChatTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private const string TestDataFilePath = "./TestData/completion_one_response.json";

    public GeminiClientChatTests()
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
        var tgs = new GeminiClient("fake-model", "fake-api-key", this._httpClient);

        // Act
        await tgs.GenerateTextAsync("fake-text");

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
        var tgs = new GeminiClient(modelId, "fake-api-key", this._httpClient);

        // Act
        await tgs.GenerateTextAsync("fake-text");

        // Assert
        Assert.Contains(modelId, this._messageHandlerStub.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldUseSpecifiedApiKeyAsync()
    {
        // Arrange
        string fakeAPIKey = "fake-api-key";
        var tgs = new GeminiClient("fake-model", fakeAPIKey, this._httpClient);

        // Act
        await tgs.GenerateTextAsync("fake-text");

        // Assert
        Assert.Contains(fakeAPIKey, this._messageHandlerStub.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldUseBaseEndpointAsync()
    {
        // Arrange
        var baseEndPoint = GeminiEndpoints.BaseEndpoint.AbsoluteUri;
        var tgs = new GeminiClient("fake-model", "fake-api-key", this._httpClient);

        // Act
        await tgs.GenerateTextAsync("fake-text");

        // Assert
        Assert.StartsWith(baseEndPoint, this._messageHandlerStub.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldSendPromptToServiceAsync()
    {
        // Arrange
        string prompt = "fake-prompt";
        var tgs = new GeminiClient("fake-model", "fake-api-key", this._httpClient);

        // Act
        await tgs.GenerateTextAsync(prompt);

        // Assert
        GeminiRequest? request = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(request);
        Assert.Equal(prompt, request.Contents[0].Parts[0].Text);
    }

    [Fact]
    public async Task ShouldReturnValidModelTextResponseAsync()
    {
        // Arrange
        var tgs = new GeminiClient("fake-model", "fake-api-key", this._httpClient);

        // Act
        IReadOnlyList<TextContent> textContents = await tgs.GenerateTextAsync("fake-text");

        // Assert
        GeminiResponse testDataResponse = JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(TestDataFilePath))!;
        var textContent = textContents.SingleOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal(testDataResponse.Candidates[0].Content.Parts[0].Text, textContent.Text);
    }

    [Fact]
    public async Task ShouldReturnValidMetadataAsync()
    {
        // Arrange
        var tgs = new GeminiClient("fake-model", "fake-api-key", this._httpClient);

        // Act
        IReadOnlyList<TextContent> textContents = await tgs.GenerateTextAsync("fake-text");

        // Assert
        GeminiResponse testDataResponse = JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(TestDataFilePath))!;
        var textContent = textContents.SingleOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal(testDataResponse.PromptFeedback!.BlockReason, textContent.Metadata!["PromptFeedbackBlockReason"]);
        Assert.Equal(testDataResponse.Candidates[0].FinishReason, textContent.Metadata!["FinishReason"]);
        Assert.Equal(testDataResponse.Candidates[0].Index, textContent.Metadata!["Index"]);
        Assert.Equal(testDataResponse.Candidates[0].TokenCount, textContent.Metadata!["TokenCount"]);
        Assert.True((textContent.Metadata!["SafetyRatings"] as IEnumerable<object>)!.Count()
                    == testDataResponse.Candidates[0].SafetyRatings.Count);
        Assert.True((textContent.Metadata!["PromptFeedbackSafetyRatings"] as IEnumerable<object>)!.Count()
                    == testDataResponse.PromptFeedback.SafetyRatings.Count);
    }

    [Fact]
    public async Task ShouldReturnResponseWithModelIdAsync()
    {
        // Arrange
        string modelId = "fake-model";
        var tgs = new GeminiClient(modelId, "fake-api-key", this._httpClient);

        // Act
        IReadOnlyList<TextContent> textContents = await tgs.GenerateTextAsync("fake-text");

        // Assert
        var textContent = textContents.SingleOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal(modelId, textContent.ModelId);
    }

    [Fact]
    public async Task ShouldReturnResponseWithValidInnerContentAsync()
    {
        // Arrange
        var tgs = new GeminiClient("fake-model", "fake-api-key", this._httpClient);

        // Act
        IReadOnlyList<TextContent> textContents = await tgs.GenerateTextAsync("fake-text");

        // Assert
        string testDataResponseJson = JsonSerializer.Serialize(JsonSerializer.Deserialize<GeminiResponse>(
            await File.ReadAllTextAsync(TestDataFilePath))!.Candidates[0]);
        var textContent = textContents.SingleOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal(testDataResponseJson, JsonSerializer.Serialize(textContent.InnerContent));
    }

    [Fact]
    public async Task ShouldUsePromptExecutionSettingsAsync()
    {
        // Arrange
        var tgs = new GeminiClient("fake-model", "fake-api-key", this._httpClient);
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 102,
            Temperature = 0.45,
            TopP = 0.6
        };

        // Act
        await tgs.GenerateTextAsync("fake-text", executionSettings);

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
