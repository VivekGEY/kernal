﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.AI.TextGeneration;

/// <summary>
/// Interface for text generation results.
/// </summary>
public interface ITextResult : IResultBase
{
    /// <summary>
    /// Asynchronously retrieves the text generation result.
    /// </summary>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, with the result being the completed text.</returns>
    Task<string> GetCompletionAsync(CancellationToken cancellationToken = default);
}
