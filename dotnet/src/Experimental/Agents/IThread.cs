﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Experimental.Agents;

/// <summary>
/// Interface for a conversable thread.
/// </summary>
public interface IThread
{
    /// <summary>
    /// Invokes the thread.
    /// </summary>
    /// <returns></returns>
    Task<string> InvokeAsync(string userMessage);

    /// <summary>
    /// Adds a system message.
    /// </summary>
    /// <param name="message">The message to add.</param>
    void AddSystemMessage(string message);

    /// <summary>
    /// Gets the chat messages.
    /// </summary>
    IReadOnlyList<ChatMessageContent> ChatMessages { get; }
}
