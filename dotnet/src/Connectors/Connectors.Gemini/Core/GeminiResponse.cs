﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Connectors.Gemini.Core;

public sealed class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public IList<GeminiResponseCandidate> Candidates { get; set; }

    [JsonPropertyName("promptFeedback")]
    public GeminiResponsePromptFeedback? PromptFeedback { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiResponseUsageMetadata? UsageMetadata { get; set; }
}

public sealed class GeminiResponseCandidate
{
    [JsonPropertyName("content")]
    public GeminiResponseContent Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("safetyRatings")]
    public IList<GeminiResponseSafetyRating> SafetyRatings { get; set; }

    [JsonPropertyName("tokenCount")]
    public int TokenCount { get; set; }
}

public sealed class GeminiResponseContent
{
    [JsonPropertyName("parts")]
    public IList<GeminiResponsePart> Parts { get; set; }

    [JsonPropertyName("role")]
    [JsonConverter(typeof(AuthorRoleConverter))]
    public AuthorRole? Role { get; set; }
}

public sealed class GeminiResponsePart
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}

public sealed class GeminiResponseSafetyRating
{
    [JsonPropertyName("category")]
    public string Category { get; set; }

    [JsonPropertyName("probability")]
    public string Probability { get; set; }

    [JsonPropertyName("block")]
    public bool Block { get; set; }
}

public sealed class GeminiResponsePromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }

    [JsonPropertyName("safetyRatings")]
    public IList<GeminiResponseSafetyRating> SafetyRatings { get; set; }
}

public sealed class GeminiResponseUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}
