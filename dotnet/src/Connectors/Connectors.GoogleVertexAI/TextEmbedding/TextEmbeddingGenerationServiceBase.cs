﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Embeddings;

namespace Microsoft.SemanticKernel.Connectors.GoogleVertexAI;

/// <summary>
/// Represents a service for generating text using the GoogleVertex AI Gemini API.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
public class TextEmbeddingGenerationServiceBase : ITextEmbeddingGenerationService
{
    private protected Dictionary<string, object?> AttributesInternal { get; } = new();
    private protected IEmbeddingsClient EmbeddingsClient { get; init; } = null!;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Attributes => this.AttributesInternal;

    /// <inheritdoc />
    public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return this.EmbeddingsClient.GenerateEmbeddingsAsync(data, cancellationToken);
    }
}
