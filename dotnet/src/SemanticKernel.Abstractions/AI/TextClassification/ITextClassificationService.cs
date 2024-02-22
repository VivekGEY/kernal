﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.TextClassification;

/// <summary>
/// Interface for text classification services.
/// </summary>
[Experimental("SKEXP0006")]
public interface ITextClassificationService : IAIService
{
    /// <summary>
    /// Classify text.
    /// </summary>
    /// <param name="texts">Texts to classify.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Classification of texts.</returns>
    Task<IReadOnlyList<ClassificationContent>> ClassifyTextAsync(
        IEnumerable<string> texts,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);
}
