﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Class with data related to prompt after rendering.
/// </summary>
[Experimental("SKEXP0005")]
public sealed class PromptRenderedContext : PromptFilterContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRenderedContext"/> class.
    /// </summary>
    /// <param name="function">The <see cref="KernelFunction"/> with which this filter is associated.</param>
    /// <param name="arguments">The arguments associated with the operation.</param>
    /// <param name="renderedPrompt">The prompt that was rendered by the associated operation.</param>
    public PromptRenderedContext(KernelFunction function, KernelArguments arguments, string renderedPrompt)
        : base(function, arguments, metadata: null)
    {
        Verify.NotNullOrWhiteSpace(renderedPrompt);

        this.RenderedPrompt = renderedPrompt;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the operation associated with
    /// the filter should be canceled.
    /// </summary>
    /// <remarks>
    /// The filter may set <see cref="Cancel"/> to true to indicate that the operation should
    /// be canceled. If there are multiple filters registered, subsequent filters
    /// may see and change a value set by a previous filter. The final result is what will
    /// be considered by the component that triggers filter.
    /// </remarks>
    public bool Cancel { get; set; }

    /// <summary>
    /// Gets or sets the rendered prompt.
    /// </summary>
    /// <remarks>
    /// The filter may view the rendered prompt and change it, if desired.
    /// If there are multiple filters registered, subsequent filters may
    /// overwrite a value set by a previous filter. The final result is what will
    /// be the prompt used by the system.
    /// </remarks>
    public string RenderedPrompt { get; set; }
}
