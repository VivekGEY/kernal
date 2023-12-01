﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.AI.ChatCompletion;

/// <summary>
/// Provides extension methods for the IChatCompletion interface.
/// </summary>
public static class ChatCompletionExtensions
{
    /// <summary>
    /// Generates a new chat message asynchronously.
    /// </summary>
    /// <param name="chatCompletion">The target IChatCompletion interface to extend.</param>
    /// <param name="chat">The chat history.</param>
    /// <param name="executionSettings">The AI request settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The asynchronous cancellation token (optional).</param>
    /// <remarks>This extension does not support multiple prompt results (only the first will be returned).</remarks>
    /// <returns>A task representing the generated chat message in string format.</returns>
    public static async Task<string> GenerateMessageAsync(
        this IChatCompletion chatCompletion,
        ChatHistory chat,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // Using var below results in Microsoft.CSharp.RuntimeBinder.RuntimeBinderException : Cannot apply indexing with [] to an expression of type 'object'
        IReadOnlyList<IChatResult> chatResults = await chatCompletion.GetChatCompletionsAsync(chat, executionSettings, kernel, cancellationToken).ConfigureAwait(false);
        var firstChatMessage = await chatResults[0].GetChatMessageAsync(cancellationToken).ConfigureAwait(false);
        return firstChatMessage.Content;
    }
}
