﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// OpenAI specialized chat completion options.
/// </summary>
public class OpenAIChatCompletionOptions : BaseServiceOptions
{
    /// <summary>
    /// The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>
    /// Custom <see cref="HttpClient"/> for HTTP requests.
    /// </summary>
    public HttpClient? HttpClient { get; init; }

    /// <summary>
    /// OpenAI specialized chat completion options.
    /// </summary>
    /// <param name="modelId">Model name.</param>
    public OpenAIChatCompletionOptions(string modelId)
    {
        this.ModelId = modelId;
    }
}

/// <summary>
/// OpenAI client specialized chat completion options.
/// </summary>
/// <param name="modelId">Model name.</param>
public class OpenAIClientChatCompletionnOptions(string modelId) : OpenAIChatCompletionOptions(modelId)
{
    /// <summary>
    /// OpenAI Organization Id (usually optional).
    /// </summary>
    public string? OrganizationId { get; init; }

    /// <summary>
    /// OpenAI API Key.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// A non-default OpenAI compatible API endpoint.
    /// </summary>
    public Uri? Endpoint { get; init; }
}

/// <summary>
/// OpenAI client specialized chat completion options.
/// </summary>
public class OpenAITextEmbeddingGenerationOptions : BaseServiceOptions
{
    /// <summary>
    /// The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>
    /// The number of dimensions the resulting output embeddings should have. Only supported in "text-embedding-3" and later models.
    /// </summary>
    public int? Dimensions { get; init; }
}

/// <summary>
/// OpenAI client specialized chat completion options.
/// </summary>
public class OpenAIClientTextEmbeddingGenerationOptions : OpenAITextEmbeddingGenerationOptions
{
    /// <summary>
    /// OpenAI Organization Id (usually optional).
    /// </summary>
    public string? OrganizationId { get; init; }

    /// <summary>
    /// OpenAI API Key.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// A non-default OpenAI compatible API endpoint.
    /// </summary>
    public Uri? Endpoint { get; init; }
}

/// <summary>
/// Base service options definition.
/// </summary>
public abstract class BaseServiceOptions
{
    /// <summary>
    /// Model name.
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// Service Id.
    /// </summary>
    public string? ServiceId { get; init; }
}
