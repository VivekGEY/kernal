﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.Anthropic.Core;

internal sealed class AnthropicToolCallContent : AnthropicContent
{
    [JsonPropertyName("id")]
    [JsonRequired]
    public string ToolId { get; set; } = null!;

    [JsonPropertyName("name")]
    [JsonRequired]
    public string FunctionName { get; set; } = null!;

    /// <summary>
    /// Optional. The function parameters and values in JSON object format.
    /// </summary>
    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Arguments { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"FunctionName={this.FunctionName}, Arguments={this.Arguments}";
    }
}
