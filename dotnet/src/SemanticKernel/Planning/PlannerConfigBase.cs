﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.SemanticKernel.Planning;

/// <summary>
/// Base class for planner configs
/// </summary>
public abstract class PlannerConfigBase
{
    /// <summary>
    /// A list of plugins to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedPlugins { get; } = new();

    /// <summary>
    /// A list of functions to exclude from the plan creation request.
    /// </summary>
    public HashSet<string> ExcludedFunctions { get; } = new();
}
