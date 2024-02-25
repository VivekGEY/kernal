﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Resources;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

public sealed class Example87_GeminiVision : BaseTest
{
    [Fact]
    public async Task RunAsync()
    {
        this.WriteLine("======== Gemini chat with vision ========");

        await GoogleAIGeminiAsync();
        await VertexAIGeminiAsync();
    }

    private async Task GoogleAIGeminiAsync()
    {
        this.WriteLine("===== Google AI Gemini API =====");

        string geminiApiKey = TestConfiguration.GoogleAI.ApiKey;
        string geminiModelId = "gemini-pro-vision";

        if (geminiApiKey is null)
        {
            this.WriteLine("Gemini credentials not found. Skipping example.");
            return;
        }

        Kernel kernel = Kernel.CreateBuilder()
            .AddGoogleAIGeminiChatCompletion(
                modelId: geminiModelId,
                apiKey: geminiApiKey)
            .Build();

        var chatHistory = new ChatHistory();
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        chatHistory.AddUserMessage(new ChatMessageContentItemCollection
        {
            new TextContent("What’s in this image?"),
            // Google AI Gemini API requires the image to be in base64 format, doesn't support URI
            // You have to always provide the mimeType for the image
            new ImageContent(new BinaryData(EmbeddedResource.ReadStream("sample_image.jpg")),
                metadata: new Dictionary<string, object?> { { "mimeType", "image/jpeg" } }),
        });

        var reply = await chatCompletionService.GetChatMessageContentAsync(chatHistory);

        WriteLine(reply.Content);
    }

    private async Task VertexAIGeminiAsync()
    {
        this.WriteLine("===== Vertex AI Gemini API =====");

        string geminiApiKey = TestConfiguration.VertexAI.ApiKey;
        string geminiModelId = "gemini-pro-vision";
        string geminiLocation = TestConfiguration.VertexAI.Location;
        string geminiProject = TestConfiguration.VertexAI.ProjectId;

        if (geminiApiKey is null || geminiLocation is null || geminiProject is null)
        {
            this.WriteLine("Gemini vertex ai credentials not found. Skipping example.");
            return;
        }

        Kernel kernel = Kernel.CreateBuilder()
            .AddVertexAIGeminiChatCompletion(
                modelId: geminiModelId,
                apiKey: geminiApiKey,
                location: geminiLocation,
                projectId: geminiProject)
            .Build();

        var chatHistory = new ChatHistory();
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        chatHistory.AddUserMessage(new ChatMessageContentItemCollection
        {
            new TextContent("What’s in this image?"),
            // Vertex AI Gemini API supports both base64 and URI format
            // You have to always provide the mimeType for the image
            new ImageContent(new BinaryData(EmbeddedResource.ReadStream("sample_image.jpg")),
                metadata: new Dictionary<string, object?> { { "mimeType", "image/jpeg" } }),
            // The Cloud Storage URI of the image to include in the prompt.
            // The bucket that stores the file must be in the same Google Cloud project that's sending the request.
            // new ImageContent(new Uri("gs://generativeai-downloads/images/scones.jpg"),
            //    metadata: new Dictionary<string, object?> { { "mimeType", "image/jpeg" } })
        });

        var reply = await chatCompletionService.GetChatMessageContentAsync(chatHistory);

        WriteLine(reply.Content);
    }

    public Example87_GeminiVision(ITestOutputHelper output) : base(output) { }
}
