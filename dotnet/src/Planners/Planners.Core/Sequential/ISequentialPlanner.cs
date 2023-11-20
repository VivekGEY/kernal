﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Diagnostics;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of Plan
namespace Microsoft.SemanticKernel.Planning;
#pragma warning restore IDE0130

/// <summary>
/// Interface for planner that uses a set of semantic functions to create a sequential plan.
/// </summary>
public interface ISequentialPlanner
{
    /// <summary>
    /// Create a plan for a goal.
    /// </summary>
    /// <param name="goal">The goal to create a plan for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="SKException">Thrown when the plan cannot be created.</exception>
    Task<Plan> CreatePlanAsync(string goal, CancellationToken cancellationToken = default);
}
