﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service.Model;

/// <summary>
/// A chat session
/// </summary>
public class ChatSession : IStorageEntity
{
    /// <summary>
    /// Chat ID that is persistent and unique.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Title of the chat.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    public ChatSession(string title)
    {
        this.Id = Guid.NewGuid().ToString();
        this.Title = title;
    }
}
