// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Model;

/// <summary>
/// The current status of a index.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IndexState
{

    None = 0,
    
    /// <summary>
    /// Enum Initializing for value: Initializing
    /// </summary>
    [EnumMember(Value = "Initializing")]
    Initializing = 1,

    /// <summary>
    /// Enum ScalingUp for value: ScalingUp
    /// </summary>
    [EnumMember(Value = "ScalingUp")]
    ScalingUp = 2,

    /// <summary>
    /// Enum ScalingDown for value: ScalingDown
    /// </summary>
    [EnumMember(Value = "ScalingDown")]
    ScalingDown = 3,

    /// <summary>
    /// Enum Terminating for value: Terminating
    /// </summary>
    [EnumMember(Value = "Terminating")]
    Terminating = 4,

    /// <summary>
    /// Enum Ready for value: Ready
    /// </summary>
    [EnumMember(Value = "Ready")]
    Ready = 5,
}
