﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.AI.TextCompletion;

/// <summary>
/// Interface for text completion services
/// </summary>
public interface ITextCompletion : IAIService
{
    /// <summary>
    /// Get completion results for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different completions results generated by the remote model</returns>
    Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(
        string text,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming completion results for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming results generated by the remote model</returns>
    IAsyncEnumerable<ITextStreamingResult> GetStreamingCompletionsAsync(
        string text,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming results for the prompt using the specified request settings.
    /// Each modality may support for different types of streaming contents.
    /// </summary>
    /// <remarks>
    /// Usage of this method with value types may be more efficient if the connector supports it.
    /// </remarks>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="prompt">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    IAsyncEnumerable<T> GetStreamingContentAsync<T>(
        string prompt,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);
}
