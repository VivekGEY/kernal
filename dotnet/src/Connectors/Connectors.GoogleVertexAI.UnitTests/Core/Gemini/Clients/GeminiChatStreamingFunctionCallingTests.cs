﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.GoogleVertexAI;
using Microsoft.SemanticKernel.Connectors.GoogleVertexAI.Core;
using Xunit;

namespace SemanticKernel.Connectors.GoogleVertexAI.UnitTests.Core.Gemini.Clients;

public sealed class GeminiChatStreamingFunctionCallingTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _responseContent;
    private readonly string _responseContentWithFunction;
    private readonly HttpMessageHandlerStub _messageHandlerStub;
    private readonly GeminiFunction _timePluginDate, _timePluginNow;
    private readonly Kernel _kernelWithFunctions;
    private const string ChatTestDataFilePath = "./TestData/chat_stream_response.json";
    private const string ChatTestDataWithFunctionFilePath = "./TestData/chat_one_function_response.json";

    public GeminiChatStreamingFunctionCallingTests()
    {
        this._responseContent = File.ReadAllText(ChatTestDataFilePath);
        this._responseContentWithFunction = File.ReadAllText(ChatTestDataWithFunctionFilePath)
            .Replace("%nameSeparator%", GeminiFunction.NameSeparator, StringComparison.Ordinal);
        this._messageHandlerStub = new HttpMessageHandlerStub();
        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(
            this._responseContent);

        this._httpClient = new HttpClient(this._messageHandlerStub, false);

        var kernelPlugin = KernelPluginFactory.CreateFromFunctions("TimePlugin", new[]
        {
            KernelFunctionFactory.CreateFromMethod((string? format = null)
                => DateTime.Now.Date.ToString(format, CultureInfo.InvariantCulture), "Date", "TimePlugin.Date"),
            KernelFunctionFactory.CreateFromMethod(()
                    => DateTime.Now.ToString("", CultureInfo.InvariantCulture), "Now", "TimePlugin.Now",
                parameters: [new KernelParameterMetadata("param1") { ParameterType = typeof(string), Description = "desc", IsRequired = false }]),
        });
        IList<KernelFunctionMetadata> functions = kernelPlugin.GetFunctionsMetadata();

        this._timePluginDate = functions[0].ToGeminiFunction();
        this._timePluginNow = functions[1].ToGeminiFunction();

        this._kernelWithFunctions = new Kernel();
        this._kernelWithFunctions.Plugins.Add(kernelPlugin);
    }

    [Fact]
    public async Task ShouldPassToolsToRequestAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.EnableFunctions([this._timePluginDate, this._timePluginNow])
        };

        // Act
        await client.StreamGenerateChatMessageAsync(chatHistory, executionSettings: executionSettings, kernel: this._kernelWithFunctions)
            .ToListAsync();

        // Assert
        GeminiRequest? request = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent);
        Assert.NotNull(request);
        Assert.NotNull(request.Tools);
        Assert.Collection(request.Tools[0].Functions,
            item => Assert.Equal(this._timePluginDate.FullyQualifiedName, item.Name),
            item => Assert.Equal(this._timePluginNow.FullyQualifiedName, item.Name));
        Assert.Collection(request.Tools[0].Functions,
            item =>
                Assert.Equal(JsonSerializer.Serialize(this._timePluginDate.ToFunctionDeclaration().Parameters),
                    JsonSerializer.Serialize(item.Parameters)),
            item =>
                Assert.Equal(JsonSerializer.Serialize(this._timePluginNow.ToFunctionDeclaration().Parameters),
                    JsonSerializer.Serialize(item.Parameters)));
    }

    [Fact]
    public async Task ShouldPassFunctionCallToRequestAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();
        var functionCallPart = new GeminiPart.FunctionCallPart
        {
            FunctionName = this._timePluginNow.FullyQualifiedName,
            Arguments = JsonSerializer.SerializeToNode(new { param1 = "hello" })
        };
        chatHistory.Add(new GeminiChatMessageContent(AuthorRole.Assistant, string.Empty, "modelId", [functionCallPart]));
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.EnableFunctions([this._timePluginDate, this._timePluginNow])
        };

        // Act
        await client.StreamGenerateChatMessageAsync(chatHistory, executionSettings: executionSettings, kernel: this._kernelWithFunctions)
            .ToListAsync();

        // Assert
        GeminiRequest request = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent)!;
        var content = request.Contents.LastOrDefault();
        Assert.NotNull(content);
        Assert.Equal(AuthorRole.Assistant, content.Role);
        var functionCall = content.Parts[0].FunctionCall;
        Assert.NotNull(functionCall);
        Assert.Equal(functionCallPart.FunctionName, functionCall.FunctionName);
        Assert.Equal(JsonSerializer.Serialize(functionCallPart.Arguments), functionCall.Arguments!.ToJsonString());
    }

    [Fact]
    public async Task ShouldPassFunctionResponseToRequestAsync()
    {
        // Arrange
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();
        var functionCallPart = new GeminiPart.FunctionCallPart
        {
            FunctionName = this._timePluginNow.FullyQualifiedName,
            Arguments = JsonSerializer.SerializeToNode(new { param1 = "hello" })
        };
        var toolCallResponse = new GeminiFunctionToolCall(new GeminiPart.FunctionCallPart()
        {
            FunctionName = this._timePluginNow.FullyQualifiedName,
            Arguments = JsonSerializer.SerializeToNode(new { time = "Time now" })
        });
        chatHistory.Add(new GeminiChatMessageContent(AuthorRole.Assistant, string.Empty, "modelId", [functionCallPart]));
        chatHistory.Add(new GeminiChatMessageContent(AuthorRole.Tool, string.Empty, "modelId", toolCallResponse));
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.EnableFunctions([this._timePluginDate, this._timePluginNow])
        };

        // Act
        await client.StreamGenerateChatMessageAsync(chatHistory, executionSettings: executionSettings, kernel: this._kernelWithFunctions)
            .ToListAsync();

        // Assert
        GeminiRequest request = JsonSerializer.Deserialize<GeminiRequest>(this._messageHandlerStub.RequestContent)!;
        var content = request.Contents.LastOrDefault();
        Assert.NotNull(content);
        Assert.Equal(AuthorRole.Tool, content.Role);
        var functionResponse = content.Parts[0].FunctionResponse;
        Assert.NotNull(functionResponse);
        Assert.Equal(toolCallResponse.FullyQualifiedName, functionResponse.FunctionName);
        Assert.Equal(JsonSerializer.Serialize(toolCallResponse.Arguments), functionResponse.ResponseArguments.ToJsonString());
    }

    [Fact]
    public async Task ShouldReturnFunctionsCalledByModelAsync()
    {
        // Arrange
        this._messageHandlerStub.ResponseToReturn.Content = new StringContent(this._responseContentWithFunction);
        var client = this.CreateChatCompletionClient();
        var chatHistory = CreateSampleChatHistory();
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.EnableFunctions([this._timePluginDate, this._timePluginNow])
        };

        // Act
        var chatMessageContents =
            await client.StreamGenerateChatMessageAsync(chatHistory, executionSettings: executionSettings, kernel: this._kernelWithFunctions)
                .ToListAsync();

        // Assert
        var message = chatMessageContents.SingleOrDefault() as GeminiStreamingChatMessageContent;
        Assert.NotNull(message);
        Assert.NotNull(message.ToolCalls);
        Assert.Single(message.ToolCalls,
            item => item.FullyQualifiedName == this._timePluginNow.FullyQualifiedName);
        Assert.Single(message.ToolCalls,
            item => item.Arguments!["param1"]!.ToString()!.Equals("hello", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IfAutoInvokeShouldAddFunctionsCalledByModelToChatHistoryAsync()
    {
        // Arrange
        using var handlerStub = new MultipleHttpMessageHandlerStub();
        handlerStub.AddJsonResponse(this._responseContentWithFunction);
        handlerStub.AddJsonResponse(this._responseContent);
#pragma warning disable CA2000
        var client = this.CreateChatCompletionClient(httpClient: handlerStub.CreateHttpClient());
#pragma warning restore CA2000
        var chatHistory = CreateSampleChatHistory();
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Act
        await client.StreamGenerateChatMessageAsync(chatHistory, executionSettings: executionSettings, kernel: this._kernelWithFunctions)
            .ToListAsync();

        // Assert
        var messages = chatHistory.OfType<GeminiChatMessageContent>();
        var contents = messages.Where(item =>
            item.Role == AuthorRole.Assistant &&
            item.ToolCalls is not null &&
            item.ToolCalls.Any(toolCall => toolCall.FullyQualifiedName == this._timePluginNow.FullyQualifiedName) &&
            item.ToolCalls.Any(toolCall => toolCall.Arguments!["param1"]!.ToString()!.Equals("hello", StringComparison.Ordinal)));
        Assert.Single(contents);
    }

    [Fact]
    public async Task IfAutoInvokeShouldAddFunctionResponseToChatHistoryAsync()
    {
        // Arrange
        using var handlerStub = new MultipleHttpMessageHandlerStub();
        handlerStub.AddJsonResponse(this._responseContentWithFunction);
        handlerStub.AddJsonResponse(this._responseContent);
#pragma warning disable CA2000
        var client = this.CreateChatCompletionClient(httpClient: handlerStub.CreateHttpClient());
#pragma warning restore CA2000
        var chatHistory = CreateSampleChatHistory();
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Act
        await client.StreamGenerateChatMessageAsync(chatHistory, executionSettings: executionSettings, kernel: this._kernelWithFunctions)
            .ToListAsync();

        // Assert
        var messages = chatHistory.OfType<GeminiChatMessageContent>();
        var contents = messages.Where(item =>
            item.Role == AuthorRole.Tool &&
            item.CalledTool is not null &&
            item.CalledTool.FullyQualifiedName == this._timePluginNow.FullyQualifiedName &&
            item.CalledTool.Arguments!.ContainsKey("response"));
        Assert.Single(contents);
    }

    [Fact]
    public async Task IfAutoInvokeShouldReturnAssistantMessagesWithContentAsync()
    {
        // Arrange
        using var handlerStub = new MultipleHttpMessageHandlerStub();
        handlerStub.AddJsonResponse(this._responseContentWithFunction);
        handlerStub.AddJsonResponse(this._responseContent);
#pragma warning disable CA2000
        var client = this.CreateChatCompletionClient(httpClient: handlerStub.CreateHttpClient());
#pragma warning restore CA2000
        var chatHistory = CreateSampleChatHistory();
        var executionSettings = new GeminiPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Act
        var messages =
            await client.StreamGenerateChatMessageAsync(chatHistory, executionSettings: executionSettings, kernel: this._kernelWithFunctions)
                .ToListAsync();

        // Assert
        Assert.All(messages, item =>
            Assert.Equal(AuthorRole.Assistant, item.Role));
        Assert.All(messages, item =>
            Assert.False(string.IsNullOrWhiteSpace(item.Content)));
    }

    private static ChatHistory CreateSampleChatHistory()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello");
        chatHistory.AddAssistantMessage("Hi");
        chatHistory.AddUserMessage("How are you?");
        return chatHistory;
    }

    private GeminiChatCompletionClient CreateChatCompletionClient(
        string modelId = "fake-model",
        HttpClient? httpClient = null,
        IEndpointProvider? endpointProvider = null,
        IHttpRequestFactory? httpRequestFactory = null)
    {
        return new GeminiChatCompletionClient(
            httpClient: httpClient ?? this._httpClient,
            modelId: modelId,
            httpRequestFactory: httpRequestFactory ?? new FakeHttpRequestFactory(),
            endpointProvider: endpointProvider ?? new FakeEndpointProvider());
    }

    public void Dispose()
    {
        this._httpClient.Dispose();
        this._messageHandlerStub.Dispose();
    }
}
