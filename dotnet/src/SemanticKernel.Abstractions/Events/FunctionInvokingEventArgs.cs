﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace Microsoft.SemanticKernel.Events;

/// <summary>
/// Event arguments available to the Kernel.FunctionInvoking event.
/// </summary>
public sealed class FunctionInvokingEventArgs : CancelEventArgs
{
    internal FunctionInvokingEventArgs(FunctionView functionView, SKContext context, string? prompt)
    {
        Verify.NotNull(context);

        this.FunctionView = functionView;
        this.SKContext = context;
        this.Prompt = prompt;
    }

    /// <summary>
    /// Function view details.
    /// </summary>
    public FunctionView FunctionView { get; }

    /// <summary>
    /// SKContext prior function execution.
    /// </summary>
    public SKContext SKContext { get; }

    /// <summary>
    /// Prompt that was rendered prior to the function execution.
    /// </summary>
    /// <remarks>
    /// May be null for native functions.
    /// </remarks>
    public string? Prompt { get; }
}
