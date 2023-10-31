﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Services;

/// <summary>
/// Represents an empty interface for AI services.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces")]
public interface IAIService
{
    /// <summary>
    /// Gets the AI service metadata.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
