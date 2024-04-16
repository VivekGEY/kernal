﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.Anthropic.Core;

internal sealed class AnthropicImageContent : AnthropicContent
{
    [JsonConstructor]
    public AnthropicImageContent(string type, string mediaType, string data)
    {
        this.Source = new SourceEntity(type, mediaType, data);
    }

    /// <summary>
    /// Only used when type is "image". The image content.
    /// </summary>
    [JsonPropertyName("source")]
    public SourceEntity Source { get; set; }

    internal sealed class SourceEntity
    {
        [JsonConstructor]
        internal SourceEntity(string type, string mediaType, string data)
        {
            this.Type = type;
            this.MediaType = mediaType;
            this.Data = data;
        }

        /// <summary>
        /// Currently supported only base64.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// The media type of the image.
        /// </summary>
        [JsonPropertyName("media_type")]
        public string MediaType { get; set; }

        /// <summary>
        /// The base64 encoded image data.
        /// </summary>
        [JsonPropertyName("data")]
        public string Data { get; set; }
    }
}
