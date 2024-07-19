﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Connectors.Amazon.Models.Mistral;

/// <summary>
/// Mistral Tool object.
/// </summary>
public class MistralTool
{
    /// <summary>
    /// The type of the tool. Currently, only function is supported.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// The associated function.
    /// </summary>
    [JsonPropertyName("function")]
    public MistralFunction Function { get; set; }

    /// <summary>
    /// Construct an instance of <see cref="MistralTool"/>.
    /// </summary>
    [JsonConstructor]
    public MistralTool(string type, MistralFunction function)
    {
        this.Type = type;
        this.Function = function;
    }
}
