﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Represent function choice behavior configuration.
/// </summary>
[Experimental("SKEXP0001")]
public sealed class FunctionChoiceBehaviorConfiguration
{
    /// <summary>
    /// Represents an AI model's decision-making strategy for calling functions.
    /// </summary>
    public FunctionChoice Choice { get; init; }

    /// <summary>
    /// The functions available for AI model.
    /// </summary>
    public IEnumerable<KernelFunction>? Functions { get; init; }

    /// <summary>
    /// The maximum number of function auto-invokes that can be made in a single user request.
    /// </summary>
    /// <remarks>
    /// After this number of iterations as part of a single user request is reached, auto-invocation
    /// will be disabled. This is a safeguard against possible runaway execution if the model routinely re-requests
    /// the same function over and over. To disable auto invocation, this can be set to 0.
    /// </remarks>
    public int? MaximumAutoInvokeAttempts { get; init; }

    /// <summary>
    /// Number of requests that are part of a single user interaction that should include this functions in the request.
    /// </summary>
    /// <remarks>
    /// This should be greater than or equal to <see cref="MaximumAutoInvokeAttempts"/>.
    /// Once this limit is reached, the functions will no longer be included in subsequent requests that are part of the user operation, e.g.
    /// if this is 1, the first request will include the functions, but the subsequent response sending back the functions' result
    /// will not include the functions for further use.
    /// </remarks>
    public int? MaximumUseAttempts { get; init; }

    /// <summary>
    /// Specifies whether validation against a specified list of functions is required before allowing the model to request a function from the kernel.
    /// </summary>
    public bool? AllowAnyRequestedKernelFunction { get; init; }
}
