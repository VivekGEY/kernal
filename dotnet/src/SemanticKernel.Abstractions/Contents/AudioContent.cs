﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Represents audio content.
/// </summary>
[Experimental("SKEXP0005")]
public class AudioContent : KernelContent
{
    /// <summary>
    /// The audio binary data.
    /// </summary>
    public ReadOnlyMemory<byte>? Data { get; set; }

    /// <summary>
    /// The audio media type.
    /// </summary>
    public string? MediaType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioContent"/> class.
    /// </summary>
    /// <param name="modelId">The model ID used to generate the content.</param>
    /// <param name="innerContent">Inner content,</param>
    /// <param name="metadata">Additional metadata</param>
    [JsonConstructor]
    public AudioContent(string? modelId = null, object? innerContent = null, IReadOnlyDictionary<string, object?>? metadata = null)
        : base(innerContent, modelId, metadata)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioContent"/> class.
    /// </summary>
    /// <param name="data">The audio binary data.</param>
    /// <param name="modelId">The model ID used to generate the content.</param>
    /// <param name="innerContent">Inner content,</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="mediaType">The audio media type</param>
    public AudioContent(
        ReadOnlyMemory<byte> data,
        string? modelId = null,
        object? innerContent = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        string? mediaType = null)
        : base(innerContent, modelId, metadata)
    {
        this.Data = data;
        this.MediaType = mediaType;
    }
}
