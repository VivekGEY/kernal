﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// Event arguments available to the Kernel.PromptRendered event.
/// </summary>
public class PromptRenderedEventArgs : SKCancelEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptRenderedEventArgs"/> class.
    /// </summary>
    /// <param name="metadata">Function metadata</param>
    /// <param name="context">Context related to the event</param>
    /// <param name="renderedPrompt">Rendered prompt</param>
    public PromptRenderedEventArgs(SKFunctionMetadata metadata, SKContext context, string renderedPrompt) : base(metadata, context)
    {
        this.RenderedPrompt = renderedPrompt;
    }

    /// <summary>
    /// Rendered prompt.
    /// </summary>
    public string RenderedPrompt { get; set; }
}
