﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.Services;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

namespace Microsoft.SemanticKernel.Connectors.AzureAIInference.Core;

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with Azure AI Inference services.
/// </summary>
internal sealed class ChatClientCore
{
    private const string ModelProvider = "azure-ai-inference";
    /// <summary>
    /// Instance of <see cref="Meter"/> for metrics.
    /// </summary>
    private static readonly Meter s_meter = new("Microsoft.SemanticKernel.Connectors.AzureAIInference");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of prompt tokens used.
    /// </summary>
    private static readonly Counter<int> s_promptTokensCounter =
        s_meter.CreateCounter<int>(
            name: "semantic_kernel.connectors.azure-ai-inference.tokens.prompt",
            unit: "{token}",
            description: "Number of prompt tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of completion tokens used.
    /// </summary>
    private static readonly Counter<int> s_completionTokensCounter =
        s_meter.CreateCounter<int>(
            name: "semantic_kernel.connectors.azure-ai-inference.tokens.completion",
            unit: "{token}",
            description: "Number of completion tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the total number of tokens used.
    /// </summary>
    private static readonly Counter<int> s_totalTokensCounter =
        s_meter.CreateCounter<int>(
            name: "semantic_kernel.connectors.azure-ai-inference.tokens.total",
            unit: "{token}",
            description: "Number of tokens used");

    /// <summary>
    /// Single space constant.
    /// </summary>
    private const string SingleSpace = " ";

    /// <summary>
    /// Non-default endpoint for Azure AI Inference API.
    /// </summary>
    private Uri? Endpoint { get; init; }

    /// <summary>
    /// Non-default endpoint for Azure AI Inference API.
    /// </summary>
    private string? ModelId { get; init; }

    /// <summary>
    /// Logger instance
    /// </summary>
    private ILogger Logger { get; init; }

    /// <summary>
    /// Azure AI Inference Client
    /// </summary>
    private ChatCompletionsClient ChatClient { get; set; }

    /// <summary>
    /// Storage for AI service attributes.
    /// </summary>
    internal Dictionary<string, object?> Attributes { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatClientCore"/> class.
    /// </summary>
    /// <param name="modelId">Optional target Model Id for endpoints that support multiple models</param>
    /// <param name="apiKey">Azure AI Inference API Key.</param>
    /// <param name="endpoint">Azure AI Inference compatible API endpoint.</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    internal ChatClientCore(
        string? modelId = null,
        string? apiKey = null,
        Uri? endpoint = null,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        this.Logger = logger ?? NullLogger.Instance;
        // Accepts the endpoint if provided, otherwise uses the default Azure AI Inference endpoint.
        this.Endpoint = endpoint ?? httpClient?.BaseAddress;
        Verify.NotNull(this.Endpoint, "endpoint or base-address");
        this.AddAttribute(AIServiceExtensions.EndpointKey, this.Endpoint.ToString());

        if (string.IsNullOrEmpty(apiKey))
        {
            // Api Key is not required, when not provided will be set to single space to avoid empty exceptions from Azure SDK AzureKeyCredential type.
            // This is a common scenario when using the Azure AI Inference service thru a Gateway that may inject the API Key.
            apiKey = SingleSpace;
        }

        if (!string.IsNullOrEmpty(modelId))
        {
            this.ModelId = modelId;
            this.AddAttribute(AIServiceExtensions.ModelIdKey, modelId);
        }

        var options = GetClientOptions(httpClient);

        this.ChatClient = new ChatCompletionsClient(this.Endpoint, new AzureKeyCredential(apiKey!), options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatClientCore"/> class using the specified Azure AI Inference Client.
    /// Note: instances created this way might not have the default diagnostics settings,
    /// it's up to the caller to configure the client.
    /// </summary>
    /// <param name="chatClient">Custom <see cref="ChatCompletionsClient"/>.</param>
    /// <param name="modelId">Target Model Id for endpoints supporting more than one</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    internal ChatClientCore(
        ChatCompletionsClient chatClient,
        string? modelId = null,
        ILogger? logger = null)
    {
        Verify.NotNull(chatClient);
        if (!string.IsNullOrEmpty(modelId))
        {
            this.ModelId = modelId;
            this.AddAttribute(AIServiceExtensions.ModelIdKey, modelId);
        }

        this.Logger = logger ?? NullLogger.Instance;
        this.ChatClient = chatClient;
    }

    /// <summary>
    /// Allows adding attributes to the client.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value.</param>
    internal void AddAttribute(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            this.Attributes.Add(key, value);
        }
    }

    /// <summary>Gets options to use for an Azure AI InferenceClient</summary>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="serviceVersion">Optional API version.</param>
    /// <returns>An instance of <see cref="ChatCompletionsClientOptions"/>.</returns>
    private static ChatCompletionsClientOptions GetClientOptions(HttpClient? httpClient, ChatCompletionsClientOptions.ServiceVersion? serviceVersion = null)
    {
        ChatCompletionsClientOptions options = serviceVersion is not null ?
            new(serviceVersion.Value) :
            new();

        options.Diagnostics.ApplicationId = HttpHeaderConstant.Values.UserAgent;

        options.AddPolicy(new AddHeaderRequestPolicy(HttpHeaderConstant.Names.SemanticKernelVersion, HttpHeaderConstant.Values.GetAssemblyVersion(typeof(ChatClientCore))), Azure.Core.HttpPipelinePosition.PerCall);

        if (httpClient is not null)
        {
            options.Transport = new HttpClientTransport(httpClient);
            options.RetryPolicy = new RetryPolicy(maxRetries: 0); // Disable retry policy if and only if a custom HttpClient is provided.
            options.Retry.NetworkTimeout = Timeout.InfiniteTimeSpan; // Disable default timeout
        }

        return options;
    }

    /// <summary>
    /// Invokes the specified request and handles exceptions.
    /// </summary>
    /// <typeparam name="T">Type of the response.</typeparam>
    /// <param name="request">Request to invoke.</param>
    /// <returns>Returns the response.</returns>
    private static async Task<T> RunRequestAsync<T>(Func<Task<T>> request)
    {
        try
        {
            return await request.Invoke().ConfigureAwait(false);
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }

    /// <summary>
    /// Invokes the specified request and handles exceptions.
    /// </summary>
    /// <typeparam name="T">Type of the response.</typeparam>
    /// <param name="request">Request to invoke.</param>
    /// <returns>Returns the response.</returns>
    private static T RunRequest<T>(Func<T> request)
    {
        try
        {
            return request.Invoke();
        }
        catch (RequestFailedException e)
        {
            throw e.ToHttpOperationException();
        }
    }

    internal async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chatHistory);

        // Convert the incoming execution settings to specialized settings.
        AzureAIInferencePromptExecutionSettings chatExecutionSettings = AzureAIInferencePromptExecutionSettings.FromExecutionSettings(executionSettings);

        ValidateMaxTokens(chatExecutionSettings.MaxTokens);

        // Create the SDK ChatCompletionOptions instance from all available information.
        ChatCompletionsOptions chatOptions = this.CreateChatCompletionsOptions(chatExecutionSettings, chatHistory, kernel, this.ModelId);

        // Make the request.
        ChatCompletions? responseData = null;
        var extraParameters = chatExecutionSettings.ExtraParameters;

        List<ChatMessageContent> responseContent;
        using (var activity = ModelDiagnostics.StartCompletionActivity(this.Endpoint, this.ModelId ?? string.Empty, ModelProvider, chatHistory, chatExecutionSettings))
        {
            try
            {
                responseData = (await RunRequestAsync(() => this.ChatClient!.CompleteAsync(chatOptions, chatExecutionSettings.ExtraParameters ?? string.Empty, cancellationToken)).ConfigureAwait(false)).Value;

                this.LogUsage(responseData.Usage);
                if (responseData.Choices.Count == 0)
                {
                    throw new KernelException("Chat completions not found");
                }
            }
            catch (Exception ex) when (activity is not null)
            {
                activity.SetError(ex);
                if (responseData != null)
                {
                    // Capture available metadata even if the operation failed.
                    activity
                        .SetResponseId(responseData.Id)
                        .SetPromptTokenUsage(responseData.Usage.PromptTokens)
                        .SetCompletionTokenUsage(responseData.Usage.CompletionTokens);
                }
                throw;
            }

            responseContent = responseData.Choices.Select(chatChoice => this.GetChatMessage(chatChoice, responseData)).ToList();
            activity?.SetCompletionResponse(responseContent, responseData.Usage.PromptTokens, responseData.Usage.CompletionTokens);
        }

        return responseContent;
    }

    internal async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chatHistory);

        AzureAIInferencePromptExecutionSettings chatExecutionSettings = AzureAIInferencePromptExecutionSettings.FromExecutionSettings(executionSettings);

        ValidateMaxTokens(chatExecutionSettings.MaxTokens);

        var chatOptions = this.CreateChatCompletionsOptions(chatExecutionSettings, chatHistory, kernel, this.ModelId);
        StringBuilder? contentBuilder = null;

        // Reset state
        contentBuilder?.Clear();

        // Stream the response.
        IReadOnlyDictionary<string, object?>? metadata = null;
        string? streamedName = null;
        ChatRole? streamedRole = default;
        CompletionsFinishReason finishReason = default;

        using (var activity = ModelDiagnostics.StartCompletionActivity(this.Endpoint, this.ModelId ?? string.Empty, ModelProvider, chatHistory, chatExecutionSettings))
        {
            StreamingResponse<StreamingChatCompletionsUpdate> response;
            try
            {
                response = await RunRequestAsync(() => this.ChatClient.CompleteStreamingAsync(chatOptions, cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception ex) when (activity is not null)
            {
                activity.SetError(ex);
                throw;
            }

            var responseEnumerator = response.ConfigureAwait(false).GetAsyncEnumerator();
            List<StreamingChatMessageContent>? streamedContents = activity is not null ? [] : null;
            try
            {
                while (true)
                {
                    try
                    {
                        if (!await responseEnumerator.MoveNextAsync())
                        {
                            break;
                        }
                    }
                    catch (Exception ex) when (activity is not null)
                    {
                        activity.SetError(ex);
                        throw;
                    }

                    StreamingChatCompletionsUpdate update = responseEnumerator.Current;
                    metadata = GetResponseMetadata(update);
                    streamedRole ??= update.Role;
                    streamedName ??= update.AuthorName;
                    finishReason = update.FinishReason ?? default;

                    AuthorRole? role = null;
                    if (streamedRole.HasValue)
                    {
                        role = new AuthorRole(streamedRole.Value.ToString());
                    }

                    StreamingChatMessageContent streamingChatMessageContent =
                        new(role: update.Role.HasValue ? new AuthorRole(update.Role.ToString()!) : null, content: update.ContentUpdate, innerContent: update, modelId: update.Model, metadata: metadata)
                        {
                            AuthorName = streamedName,
                            Role = role,
                            Metadata = metadata,
                        };

                    streamedContents?.Add(streamingChatMessageContent);
                    yield return streamingChatMessageContent;
                }
            }
            finally
            {
                activity?.EndStreaming(streamedContents, null);
                await responseEnumerator.DisposeAsync();
            }
        }
    }

    private static void ValidateMaxTokens(int? maxTokens)
    {
        if (maxTokens.HasValue && maxTokens < 1)
        {
            throw new ArgumentException($"MaxTokens {maxTokens} is not valid, the value must be greater than zero");
        }
    }

    private ChatCompletionsOptions CreateChatCompletionsOptions(
        AzureAIInferencePromptExecutionSettings executionSettings,
        ChatHistory chatHistory,
        Kernel? kernel,
        string? modelId)
    {
        if (this.Logger.IsEnabled(LogLevel.Trace))
        {
            this.Logger.LogTrace("ChatHistory: {ChatHistory}, Settings: {Settings}",
                JsonSerializer.Serialize(chatHistory),
                JsonSerializer.Serialize(executionSettings));
        }

        var options = new ChatCompletionsOptions
        {
            MaxTokens = executionSettings.MaxTokens,
            Temperature = executionSettings.Temperature,
            NucleusSamplingFactor = executionSettings.NucleusSamplingFactor,
            FrequencyPenalty = executionSettings.FrequencyPenalty,
            PresencePenalty = executionSettings.PresencePenalty,
            Model = modelId,
            Seed = executionSettings.Seed,
        };

        switch (executionSettings.ResponseFormat)
        {
            case ChatCompletionsResponseFormat formatObject:
                // If the response format is an Azure SDK ChatCompletionsResponseFormat, just pass it along.
                options.ResponseFormat = formatObject;
                break;

            case string formatString:
                // If the response format is a string, map the ones we know about, and ignore the rest.
                switch (formatString)
                {
                    case "json_object":
                        options.ResponseFormat = new ChatCompletionsResponseFormatJSON();
                        break;

                    case "text":
                        options.ResponseFormat = new ChatCompletionsResponseFormatText();
                        break;
                }
                break;

            case JsonElement formatElement:
                // This is a workaround for a type mismatch when deserializing a JSON into an object? type property.
                // Handling only string formatElement.
                if (formatElement.ValueKind == JsonValueKind.String)
                {
                    string formatString = formatElement.GetString() ?? "";
                    switch (formatString)
                    {
                        case "json_object":
                            options.ResponseFormat = new ChatCompletionsResponseFormatJSON();
                            break;

                        case "text":
                            options.ResponseFormat = new ChatCompletionsResponseFormatText();
                            break;
                    }
                }
                break;
        }

        if (executionSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in executionSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        foreach (var message in chatHistory)
        {
            options.Messages.AddRange(GetRequestMessages(message));
        }

        return options;
    }

    private static List<ChatRequestMessage> GetRequestMessages(ChatMessageContent message)
    {
        if (message.Role == AuthorRole.System)
        {
            return [new ChatRequestSystemMessage(message.Content)];
        }

        if (message.Role == AuthorRole.User)
        {
            if (message.Items is { Count: 1 } && message.Items.FirstOrDefault() is TextContent textContent)
            {
                // Name removed temporarily as the Azure AI Inference service does not support it ATM.
                // Issue: https://github.com/Azure/azure-sdk-for-net/issues/45415
                return [new ChatRequestUserMessage(textContent.Text) /*{ Name = message.AuthorName }*/ ];
            }

            return [new ChatRequestUserMessage(message.Items.Select(static (KernelContent item) => (ChatMessageContentItem)(item switch
            {
                TextContent textContent => new ChatMessageTextContentItem(textContent.Text),
                ImageContent imageContent => GetImageContentItem(imageContent),
                _ => throw new NotSupportedException($"Unsupported chat message content type '{item.GetType()}'.")
            })))

            // Name removed temporarily as the Azure AI Inference service does not support it ATM.
            // Issue: https://github.com/Azure/azure-sdk-for-net/issues/45415
            /*{ Name = message.AuthorName }*/];
        }

        if (message.Role == AuthorRole.Assistant)
        {
            // Name removed temporarily as the Azure AI Inference service does not support it ATM.
            // Issue: https://github.com/Azure/azure-sdk-for-net/issues/45415
            return [new ChatRequestAssistantMessage() { Content = message.Content /* Name = message.AuthorName */ }];
        }

        throw new NotSupportedException($"Role {message.Role} is not supported.");
    }

    private static ChatMessageImageContentItem GetImageContentItem(ImageContent imageContent)
    {
        if (imageContent.Data is { IsEmpty: false } data)
        {
            return new ChatMessageImageContentItem(BinaryData.FromBytes(data), imageContent.MimeType);
        }

        if (imageContent.Uri is not null)
        {
            return new ChatMessageImageContentItem(imageContent.Uri);
        }

        throw new ArgumentException($"{nameof(ImageContent)} must have either Data or a Uri.");
    }

    /// <summary>
    /// Captures usage details, including token information.
    /// </summary>
    /// <param name="usage">Instance of <see cref="CompletionsUsage"/> with usage details.</param>
    private void LogUsage(CompletionsUsage usage)
    {
        if (usage is null)
        {
            this.Logger.LogDebug("Token usage information unavailable.");
            return;
        }

        if (this.Logger.IsEnabled(LogLevel.Information))
        {
            this.Logger.LogInformation(
                "Prompt tokens: {PromptTokens}. Completion tokens: {CompletionTokens}. Total tokens: {TotalTokens}.",
                usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
        }

        s_promptTokensCounter.Add(usage.PromptTokens);
        s_completionTokensCounter.Add(usage.CompletionTokens);
        s_totalTokensCounter.Add(usage.TotalTokens);
    }

    private ChatMessageContent GetChatMessage(ChatChoice chatChoice, ChatCompletions responseData)
    {
        var message = new ChatMessageContent(new AuthorRole(chatChoice.Message.Role.ToString()), chatChoice.Message.Content, responseData.Model, GetChatChoiceMetadata(responseData, chatChoice));

        return message;
    }

    private static Dictionary<string, object?> GetChatChoiceMetadata(ChatCompletions completions, ChatChoice chatChoice)
    {
        return new Dictionary<string, object?>(5)
        {
            { nameof(completions.Id), completions.Id },
            { nameof(completions.Created), completions.Created },
            { nameof(completions.Usage), completions.Usage },

            // Serialization of this struct behaves as an empty object {}, need to cast to string to avoid it.
            { nameof(chatChoice.FinishReason), chatChoice.FinishReason?.ToString() },
            { nameof(chatChoice.Index), chatChoice.Index },
        };
    }

    private static Dictionary<string, object?> GetResponseMetadata(StreamingChatCompletionsUpdate completions)
    {
        return new Dictionary<string, object?>(3)
        {
            { nameof(completions.Id), completions.Id },
            { nameof(completions.Created), completions.Created },

            // Serialization of this struct behaves as an empty object {}, need to cast to string to avoid it.
            { nameof(completions.FinishReason), completions.FinishReason?.ToString() },
        };
    }
}
