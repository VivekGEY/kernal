﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Anthropic;
using Microsoft.SemanticKernel.Connectors.Anthropic.Core;
using Xunit;

namespace SemanticKernel.Connectors.Anthropic.UnitTests.Core.Claude;

public sealed class ClaudeRequestTests
{
    [Fact]
    public void FromChatHistoryItReturnsClaudeRequestWithConfiguration()
    {
        // Arrange
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage("user-message");
        chatHistory.AddAssistantMessage("assist-message");
        chatHistory.AddUserMessage("user-message2");
        var executionSettings = new ClaudePromptExecutionSettings
        {
            Temperature = 1.5,
            MaxTokens = 10,
            TopP = 0.9f,
            ModelId = "claude"
        };

        // Act
        var request = ClaudeRequest.FromChatHistoryAndExecutionSettings(chatHistory, executionSettings);

        // Assert
        Assert.Equal(executionSettings.Temperature, request.Temperature);
        Assert.Equal(executionSettings.MaxTokens, request.MaxTokens);
        Assert.Equal(executionSettings.TopP, request.TopP);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FromChatHistoryItReturnsClaudeRequestWithValidStreamingMode(bool streamMode)
    {
        // Arrange
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage("user-message");
        chatHistory.AddAssistantMessage("assist-message");
        chatHistory.AddUserMessage("user-message2");
        var executionSettings = new ClaudePromptExecutionSettings
        {
            Temperature = 1.5,
            MaxTokens = 10,
            TopP = 0.9f,
            ModelId = "claude"
        };

        // Act
        var request = ClaudeRequest.FromChatHistoryAndExecutionSettings(chatHistory, executionSettings, streamMode);

        // Assert
        Assert.Equal(streamMode, request.Stream);
    }

    [Fact]
    public void FromChatHistoryItReturnsClaudeRequestWithChatHistory()
    {
        // Arrange
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage("user-message");
        chatHistory.AddAssistantMessage("assist-message");
        chatHistory.AddUserMessage("user-message2");
        var executionSettings = new ClaudePromptExecutionSettings()
        {
            ModelId = "claude",
            MaxTokens = 128,
        };

        // Act
        var request = ClaudeRequest.FromChatHistoryAndExecutionSettings(chatHistory, executionSettings);

        // Assert
        Assert.All(request.Messages, c => Assert.IsType<ClaudeTextContent>(c.Contents[0]));
        Assert.Collection(request.Messages,
            c => Assert.Equal(chatHistory[0].Content, ((ClaudeTextContent)c.Contents[0]).Text),
            c => Assert.Equal(chatHistory[1].Content, ((ClaudeTextContent)c.Contents[0]).Text),
            c => Assert.Equal(chatHistory[2].Content, ((ClaudeTextContent)c.Contents[0]).Text));
        Assert.Collection(request.Messages,
            c => Assert.Equal(chatHistory[0].Role, c.Role),
            c => Assert.Equal(chatHistory[1].Role, c.Role),
            c => Assert.Equal(chatHistory[2].Role, c.Role));
    }

    [Fact]
    public void FromChatHistoryTextAsTextContentItReturnsClaudeRequestWithChatHistory()
    {
        // Arrange
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage("user-message");
        chatHistory.AddAssistantMessage("assist-message");
        chatHistory.AddUserMessage(contentItems: [new TextContent("user-message2")]);
        var executionSettings = new ClaudePromptExecutionSettings()
        {
            ModelId = "claude",
            MaxTokens = 128,
        };

        // Act
        var request = ClaudeRequest.FromChatHistoryAndExecutionSettings(chatHistory, executionSettings);

        // Assert
        Assert.All(request.Messages, c => Assert.IsType<ClaudeTextContent>(c.Contents[0]));
        Assert.Collection(request.Messages,
            c => Assert.Equal(chatHistory[0].Content, ((ClaudeTextContent)c.Contents[0]).Text),
            c => Assert.Equal(chatHistory[1].Content, ((ClaudeTextContent)c.Contents[0]).Text),
            c => Assert.Equal(chatHistory[2].Items.Cast<TextContent>().Single().Text, ((ClaudeTextContent)c.Contents[0]).Text));
    }

    [Fact]
    public void FromChatHistoryImageAsImageContentItReturnsClaudeRequestWithChatHistory()
    {
        // Arrange
        ReadOnlyMemory<byte> imageAsBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage("user-message");
        chatHistory.AddAssistantMessage("assist-message");
        chatHistory.AddUserMessage(contentItems:
            [new ImageContent(imageAsBytes) { MimeType = "image/png" }]);
        var executionSettings = new ClaudePromptExecutionSettings()
        {
            ModelId = "claude",
            MaxTokens = 128,
        };

        // Act
        var request = ClaudeRequest.FromChatHistoryAndExecutionSettings(chatHistory, executionSettings);

        // Assert
        Assert.Collection(request.Messages,
            c => Assert.IsType<ClaudeTextContent>(c.Contents[0]),
            c => Assert.IsType<ClaudeTextContent>(c.Contents[0]),
            c => Assert.IsType<ClaudeImageContent>(c.Contents[0]));
        Assert.Collection(request.Messages,
            c => Assert.Equal(chatHistory[0].Content, ((ClaudeTextContent)c.Contents[0]).Text),
            c => Assert.Equal(chatHistory[1].Content, ((ClaudeTextContent)c.Contents[0]).Text),
            c =>
            {
                Assert.Equal(chatHistory[2].Items.Cast<ImageContent>().Single().MimeType, ((ClaudeImageContent)c.Contents[0]).Source.MediaType);
                Assert.True(imageAsBytes.ToArray().SequenceEqual(Convert.FromBase64String(((ClaudeImageContent)c.Contents[0]).Source.Data)));
            });
    }

    [Fact]
    public void FromChatHistoryUnsupportedContentItThrowsNotSupportedException()
    {
        // Arrange
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage("user-message");
        chatHistory.AddAssistantMessage("assist-message");
        chatHistory.AddUserMessage(contentItems: [new DummyContent("unsupported-content")]);
        var executionSettings = new ClaudePromptExecutionSettings()
        {
            ModelId = "claude",
            MaxTokens = 128,
        };

        // Act
        void Act() => ClaudeRequest.FromChatHistoryAndExecutionSettings(chatHistory, executionSettings);

        // Assert
        Assert.Throws<NotSupportedException>(Act);
    }

    [Fact]
    public void AddFunctionItAddsFunctionToClaudeRequest()
    {
        // Arrange
        var request = new ClaudeRequest();
        var function = new ClaudeFunction("function-name", "function-description", "desc", null, null);

        // Act
        request.AddFunction(function);

        // Assert
        Assert.Collection(request.Tools,
            func => Assert.Equivalent(function.ToFunctionDeclaration(), func, strict: true));
    }

    [Fact]
    public void AddMultipleFunctionsItAddsFunctionsToClaudeRequest()
    {
        // Arrange
        var request = new ClaudeRequest();
        var functions = new[]
        {
            new ClaudeFunction("function-name", "function-description", "desc", null, null),
            new ClaudeFunction("function-name2", "function-description2", "desc2", null, null)
        };

        // Act
        request.AddFunction(functions[0]);
        request.AddFunction(functions[1]);

        // Assert
        Assert.Collection(request.Tools,
            func => Assert.Equivalent(functions[0].ToFunctionDeclaration(), func, strict: true),
            func => Assert.Equivalent(functions[1].ToFunctionDeclaration(), func, strict: true));
    }

    private sealed class DummyContent : KernelContent
    {
        public DummyContent(object? innerContent, string? modelId = null, IReadOnlyDictionary<string, object?>? metadata = null)
            : base(innerContent, modelId, metadata) { }
    }
}
