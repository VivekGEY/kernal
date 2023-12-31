﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Gemini.Core;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.Connectors.Gemini;

/// <summary>
/// Represents a service for generating text embeddings using the Gemini API.
/// </summary>
public sealed class GeminiTextEmbeddingGenerationService : ITextEmbeddingGenerationService
{
    private readonly Dictionary<string, object?> _attributes = new();
    private readonly GeminiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiTextEmbeddingGenerationService"/> class.
    /// </summary>
    /// <param name="model">The model identifier.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="httpClient">The optional HTTP client.</param>
    public GeminiTextEmbeddingGenerationService(string model, string apiKey, HttpClient? httpClient = null)
    {
        Verify.NotNullOrWhiteSpace(model);
        Verify.NotNullOrWhiteSpace(apiKey);

        this._client = new GeminiClient(model, apiKey, HttpClientProvider.GetHttpClient(httpClient));
        this._attributes.Add(AIServiceExtensions.ModelIdKey, model);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Attributes => this._attributes;

    /// <inheritdoc />
    public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return this._client.GenerateEmbeddingsAsync(data, cancellationToken);
    }
}
