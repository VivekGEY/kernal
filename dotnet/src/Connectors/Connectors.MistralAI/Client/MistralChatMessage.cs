﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.MistralAI.Client;

/// <summary>
/// Chat message for MistralAI.
/// </summary>
internal class MistralChatMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    /// <summary>
    /// Construct an instance of <see cref="MistralChatMessage"/>.
    /// </summary>
    /// <param name="role">If provided must be one of: system, user, assistant</param>
    /// <param name="content">Content of the chat message</param>
    [JsonConstructor]
    internal MistralChatMessage(string? role, string content)
    {
        if (role is not null && role is not "system" && role is not "user" && role is not "assistant")
        {
            throw new System.ArgumentException($"Role must be one of: system, user, assistant. {role} is an invalid role.", nameof(role));
        }

        this.Role = role;
        this.Content = content;
    }
}
