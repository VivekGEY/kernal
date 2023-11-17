﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.AI;

/// <summary>
/// Represents a single update to a streaming result.
/// </summary>
public abstract class StreamingResultChunk
{
    /// <summary>
    /// Type of the update.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// In a scenario of multiple results, this represents zero-based index of the result in the streaming sequence
    /// </summary>
    public abstract int ChoiceIndex { get; }

    /// <summary>
    /// Converts the update class to string.
    /// </summary>
    /// <returns>String representation of the update</returns>
    public abstract override string ToString();

    /// <summary>
    /// Converts the update class to byte array.
    /// </summary>
    /// <returns>Byte array representation of the update</returns>
    public abstract byte[] ToByteArray();

    /// <summary>
    /// Internal chunk object reference. (Breaking glass).
    /// Each connector will have its own internal object representing the result chunk.
    /// </summary>
    /// <remarks>
    /// The usage of this property is considered "unsafe". Use it only if strictly necessary.
    /// </remarks>
    public object? InnerResultChunk { get; }

    /// <summary>
    /// The current context associated the function call.
    /// </summary>
    [JsonIgnore]
    internal SKContext? Context { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingResultChunk"/> class.
    /// </summary>
    /// <param name="innerResultChunk">Inner result chunk object reference</param>
    protected StreamingResultChunk(object? innerResultChunk = null)
    {
        this.InnerResultChunk = innerResultChunk;
    }
}
