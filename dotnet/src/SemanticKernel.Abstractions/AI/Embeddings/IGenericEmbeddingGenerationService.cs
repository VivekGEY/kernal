﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.Embeddings;

/// <summary>
/// Represents a generator of embeddings.
/// </summary>
/// <typeparam name="TValue">The type from which embeddings will be generated.</typeparam>
/// <typeparam name="TEmbedding">The numeric type of the embedding data.</typeparam>
[Experimental("SKEXP0001")]
public interface IGenericEmbeddingGenerationService<TValue, TEmbedding> : IAIService
    where TEmbedding : unmanaged
{
    /// <summary>
    /// Generates an embedding from the given <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Data collection to generate embeddings for</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of embeddings</returns>
    Task<IReadOnlyList<EmbeddingContent<TEmbedding>>> GenerateEmbeddingsAsync(
        IList<TValue> data,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);
}
