﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;
using OpenAI;

/* Phase 06

*/

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
#pragma warning disable RCS1155 // Use StringComparison when comparing strings

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Azure OpenAI chat completion service.
/// </summary>
public sealed class OpenAIChatCompletionService : IChatCompletionService, ITextGenerationService
{
    /// <summary>Core implementation shared by Azure OpenAI clients.</summary>
    private readonly ClientCore _core;

    /// <summary>
    /// Create an instance of the OpenAI chat completion connector
    /// </summary>
    /// <param name="modelId">Model name</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="organization">OpenAI Organization Id (usually optional)</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public OpenAIChatCompletionService(
        string modelId,
        string apiKey,
        string? organization = null,
        HttpClient? httpClient = null,
        ILoggerFactory? loggerFactory = null
)
    {
        this._core = new(
            modelId,
            apiKey,
            organization,
            endpoint: null,
            httpClient,
            loggerFactory?.CreateLogger(typeof(OpenAIChatCompletionService)));
    }

    /// <summary>
    /// Create an instance of the Custom Message API OpenAI chat completion connector
    /// </summary>
    /// <param name="modelId">Model name</param>
    /// <param name="endpoint">Custom Message API compatible endpoint</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="organization">OpenAI Organization Id (usually optional)</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    [Experimental("SKEXP0010")]
    public OpenAIChatCompletionService(
            string modelId,
            Uri endpoint,
            string? apiKey = null,
            string? organization = null,
            HttpClient? httpClient = null,
            ILoggerFactory? loggerFactory = null)
    {
        Uri? internalClientEndpoint = null;
        var providedEndpoint = endpoint ?? httpClient?.BaseAddress;
        if (providedEndpoint is not null)
        {
            // If the provided endpoint does not provide a path, we add a version to the base path for compatibility
            if (providedEndpoint.PathAndQuery.Length == 0 || providedEndpoint.PathAndQuery == "/")
            {
                internalClientEndpoint = new Uri(providedEndpoint, "/v1/");
            }
            else
            {
                // As OpenAI Client automatically adds the chatcompletions endpoint, we remove it to avoid duplication.
                const string PathAndQueryPattern = "/chat/completions";
                var providedEndpointText = providedEndpoint.ToString();
                int index = providedEndpointText.IndexOf(PathAndQueryPattern, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    internalClientEndpoint = new Uri($"{providedEndpointText.Substring(0, index)}{providedEndpointText.Substring(index + PathAndQueryPattern.Length)}");
                }
                else
                {
                    internalClientEndpoint = providedEndpoint;
                }
            }
        }

        this._core = new(
            modelId,
            apiKey,
            organization,
            internalClientEndpoint,
            httpClient,
            loggerFactory?.CreateLogger(typeof(OpenAIChatCompletionService)));
    }

    /// <summary>
    /// Create an instance of the OpenAI chat completion connector
    /// </summary>
    /// <param name="modelId">Model name</param>
    /// <param name="openAIClient">Custom <see cref="OpenAIClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public OpenAIChatCompletionService(
        string modelId,
        OpenAIClient openAIClient,
        ILoggerFactory? loggerFactory = null)
    {
        this._core = new(
            modelId,
            openAIClient,
            loggerFactory?.CreateLogger(typeof(OpenAIChatCompletionService)));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes => this._core.Attributes;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        => this._core.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        => this._core.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TextContent>> GetTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        => this._core.GetChatAsTextContentsAsync(prompt, executionSettings, kernel, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        => this._core.GetChatAsTextStreamingContentsAsync(prompt, executionSettings, kernel, cancellationToken);
}
