﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Gemini;
using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Gemini.ChatCompletion;

public class GeminiChatCompletionTests
{
    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    [Fact(Skip = "This test is for manual verification.")]
    public async Task GeminiChatGenerationAsync()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello, I'm Brandon, how are you?");
        chatHistory.AddAssistantMessage("I'm doing well, thanks for asking.");
        chatHistory.AddUserMessage("Call me by my name and expand this abbreviation: LLM");

        var geminiService = new GeminiChatCompletionService(this.GetModel(), this.GetApiKey());

        // Act
        var response = await geminiService.GetChatMessageContentAsync(chatHistory);

        // Assert
        Assert.NotNull(response.Content);
        Assert.Contains("Large Language Model", response.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Brandon", response.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "This test is for manual verification.")]
    public async Task GeminiChatStreamingAsync()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello, I'm Brandon, how are you?");
        chatHistory.AddAssistantMessage("I'm doing well, thanks for asking.");
        chatHistory.AddUserMessage("Call me by my name and write a long story about my name.");

        var geminiService = new GeminiChatCompletionService(this.GetModel(), this.GetApiKey());

        // Act
        var response =
            await geminiService.GetStreamingChatMessageContentsAsync(chatHistory).ToListAsync();

        // Assert
        Assert.NotEmpty(response);
        Assert.True(response.Count > 1);
        Assert.DoesNotContain(response, chatMessage => string.IsNullOrEmpty(chatMessage.Content));
    }

    private string GetModel() => this._configuration.GetSection("Gemini:ModelId").Get<string>()!;
    private string GetApiKey() => this._configuration.GetSection("Gemini:ApiKey").Get<string>()!;
}
