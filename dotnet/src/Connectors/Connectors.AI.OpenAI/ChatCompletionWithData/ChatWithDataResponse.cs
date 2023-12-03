﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletionWithData;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes

[Experimental("SKEXP0010")]
internal sealed class ChatWithDataResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public int Created { get; set; } = default;

    [JsonPropertyName("choices")]
    public IList<ChatWithDataChoice> Choices { get; set; } = Array.Empty<ChatWithDataChoice>();

    [JsonPropertyName("usage")]
    public ChatWithDataUsage Usage { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonConstructor]
    public ChatWithDataResponse(ChatWithDataUsage usage)
    {
        this.Usage = usage;
    }
}
