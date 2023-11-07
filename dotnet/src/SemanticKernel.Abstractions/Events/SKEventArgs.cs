﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// Base arguments for events.
/// </summary>
public abstract class SKEventArgs : EventArgs
{
    /// <summary>
    /// Metadata key for storing the rendered prompt when available.
    /// </summary>
    public const string RenderedPromptMetadataKey = "RenderedPrompt";

    /// <summary>
    /// Initializes a new instance of the <see cref="SKEventArgs"/> class.
    /// </summary>
    /// <param name="functionView">Function view details</param>
    /// <param name="context">Context related to the event</param>
    internal SKEventArgs(FunctionView functionView, SKContext context)
    {
        Verify.NotNull(context);
        Verify.NotNull(functionView);

        this.FunctionView = functionView;
        this.SKContext = context;
        this.Metadata = new();
    }

    /// <summary>
    /// Function view details.
    /// </summary>
    public FunctionView FunctionView { get; }

    /// <summary>
    /// Context related to the event.
    /// </summary>
    public SKContext SKContext { get; }

    /// <summary>
    /// Metadata for storing additional information about function execution result.
    /// </summary>
    public Dictionary<string, object> Metadata { get; protected set; }
}
