﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextEmbedding;

/// <summary>
/// OpenAI text embedding service.
/// </summary>
public sealed class OpenAITextEmbeddingGeneration : OpenAIClientBase, ITextEmbeddingGeneration
{
    private readonly OpenAIServiceAttributes _attributes;

    /// <summary>
    /// Create an instance of the OpenAI text embedding connector
    /// </summary>
    /// <param name="modelId">Model name</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="organization">OpenAI Organization Id (usually optional)</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    public OpenAITextEmbeddingGeneration(
        string modelId,
        string apiKey,
        string? organization = null,
        HttpClient? httpClient = null,
        ILoggerFactory? loggerFactory = null
    ) : base(modelId, apiKey, organization, httpClient, loggerFactory)
    {
        this._attributes = new OpenAIServiceAttributes()
        {
            ModelId = modelId
        };
    }

    /// <inheritdoc/>
    public T? GetAttributes<T>() where T : AIServiceAttributes
    {
        return this._attributes as T;
    }

    /// <summary>
    /// Generates an embedding from the given <paramref name="data"/>.
    /// </summary>
    /// <param name="data">List of strings to generate embeddings for</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of embeddings</returns>
    public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        CancellationToken cancellationToken = default)
    {
        this.LogActionDetails();
        return this.InternalGetEmbeddingsAsync(data, cancellationToken);
    }
}
