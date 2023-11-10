﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Planners.Model;

/// <summary>
/// A class to describe an SKFunction in a Json Schema friendly way.
/// </summary>
public sealed class JsonSchemaFunctionManual
{
    /// <summary>
    /// he function name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The function description.
    /// </summary>

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;


    /// <summary>
    /// The function parameters.
    /// </summary>
    [JsonPropertyName("parameters")]
    public JsonSchemaFunctionParameters Parameters { get; set; } = new JsonSchemaFunctionParameters();

    /// <summary>
    /// The function response.
    /// </summary>

    [JsonPropertyName("responses")]
    public Dictionary<string, JsonSchemaFunctionResponse> FunctionResponses { get; set; } = new Dictionary<string, JsonSchemaFunctionResponse>();
}
