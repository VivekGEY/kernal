﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.AI;

/// <summary>
/// Base class for all AI results
/// </summary>
public abstract class ModelContent
{
    /// <summary>
    /// Raw content object reference. (Breaking glass).
    /// </summary>
    public object? InnerContent { get; }

    /// <summary>
    /// The metadata associated with the content.
    /// </summary>
    [JsonExtensionData]
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelContent"/> class.
    /// </summary>
    /// <param name="innerContent">Raw content object reference</param>
    /// <param name="metadata">Metadata associated with the content</param>
    protected ModelContent(object? innerContent, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        this.InnerContent = innerContent;
        this.Metadata = metadata ?? new Dictionary<string, object?>();
    }
}
