﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Planners.Model;

/// <summary>
/// A class to describe the content of a response/return type from an SKFunction, in a Json Schema friendly way.
/// </summary>
public sealed class JsonSchemaFunctionContent
{
    /// <summary>
    /// The Json Schema for applivation/json responses.
    /// </summary>
    [JsonPropertyName("application/json")]
    public JsonSchemaWrapper JsonSchemaWrapper { get; } = new JsonSchemaWrapper();
}
