﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using SemanticKernel.Service.Options;

namespace SemanticKernel.Service.CopilotChat.Options;

/// <summary>
/// Configuration options for the planner.
/// </summary>
public class PlannerOptions
{
    public const string PropertyName = "Planner";

    /// <summary>
    /// Whether to enable the planner.
    /// </summary>
    public bool Enabled { get; set; } = false;
}
