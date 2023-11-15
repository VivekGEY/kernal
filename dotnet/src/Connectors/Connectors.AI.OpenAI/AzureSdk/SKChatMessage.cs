﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

/// <summary>
/// Chat message representation from Semantic Kernel ChatMessage Abstraction
/// </summary>
public class SKChatMessage : SemanticKernel.AI.ChatCompletion.ChatMessage
{
    private readonly Azure.AI.OpenAI.ChatMessage? _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="SKChatMessage"/> class.
    /// </summary>
    /// <param name="message">OpenAI SDK chat message representation</param>
    public SKChatMessage(Azure.AI.OpenAI.ChatMessage message)
        : base(new AuthorRole(message.Role.ToString()), message.Content)
    {
        this._message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SKChatMessage"/> class.
    /// </summary>
    /// <param name="role">Role of the author of the message.</param>
    /// <param name="content">Content of the message.</param>
    public SKChatMessage(string role, string content)
        : base(new AuthorRole(role), content)
    {
    }

    /// <summary>
    /// Exposes the underlying OpenAI SDK function call chat message representation
    /// </summary>
    public FunctionCall FunctionCall
        => this._message?.FunctionCall ?? throw new NotSupportedException("Function call is not supported");
}
