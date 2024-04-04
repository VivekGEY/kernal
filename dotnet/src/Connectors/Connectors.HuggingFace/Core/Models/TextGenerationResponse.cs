﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Microsoft.SemanticKernel.Connectors.HuggingFace.Client.Models.TextGenerationResponse;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes

namespace Microsoft.SemanticKernel.Connectors.HuggingFace.Client.Models;

internal sealed class TextGenerationResponse : List<GeneratedTextItem>
{
    internal sealed class GeneratedTextItem
    {
        /// <summary>
        /// The continuated string
        /// </summary>
        [JsonPropertyName("generated_text")]
        public string? GeneratedText { get; set; }
    }

    internal class TextGenerationPrefillToken
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("logprob")]
        public double LogProb { get; set; }
    }

    internal sealed class TextGenerationToken : TextGenerationPrefillToken
    {
        [JsonPropertyName("special")]
        public bool Special { get; set; }
    }

    internal sealed class TextGenerationDetails
    {
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("generated_tokens")]
        public int GeneratedTokens { get; set; }

        [JsonPropertyName("seed")]
        public long? Seed { get; set; }

        [JsonPropertyName("prefill")]
        public string? Prefill { get; set; }
    }
}
