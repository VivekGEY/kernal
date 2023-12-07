﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.ChatCompletion;

/// <summary>
/// Class sponsor that holds extension methods for <see cref="IChatCompletionService"/> interface.
/// </summary>
public static class ChatCompletionServiceExtensions
{
    /// <summary>
    /// Get chat multiple chat message content choices for the prompt and settings.
    /// </summary>
    /// <remarks>
    /// This should be used when the settings request for more than one choice.
    /// </remarks>
    /// <param name="chatCompletionService">Target chat completion service.</param>
    /// <param name="prompt">The standardized prompt input.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat message content choices generated by the remote model</returns>
    public static Task<IReadOnlyList<ChatMessage>> GetChatMessagesAsync(
        this IChatCompletionService chatCompletionService,
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // Try to parse the text as a chat history
        if (ChatPromptParser.TryParse(prompt, out var chatHistory))
        {
            return chatCompletionService.GetChatMessagesAsync(chatHistory, executionSettings, kernel, cancellationToken);
        }

        //Otherwise, use the prompt as the chat system message
        return chatCompletionService.GetChatMessagesAsync(new ChatHistory(prompt), executionSettings, kernel, cancellationToken);
    }

    /// <summary>
    /// Get a single chat message content for the prompt and settings.
    /// </summary>
    /// <param name="chatCompletionService">The target IChatCompletionSErvice interface to extend.</param>
    /// <param name="prompt">The standardized prompt input.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat results generated by the remote model</returns>
    public static async Task<ChatMessage> GetChatMessageAsync(
        this IChatCompletionService chatCompletionService,
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => (await chatCompletionService.GetChatMessagesAsync(prompt, executionSettings, kernel, cancellationToken).ConfigureAwait(false))
            .Single();

    /// <summary>
    /// Get a single chat message content for the chat history and settings provided.
    /// </summary>
    /// <param name="chatCompletionService">The target IChatCompletionService interface to extend.</param>
    /// <param name="chatHistory">The chat history to complete.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat results generated by the remote model</returns>
    public static async Task<ChatMessage> GetChatMessageAsync(
        this IChatCompletionService chatCompletionService,
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => (await chatCompletionService.GetChatMessagesAsync(chatHistory, executionSettings, kernel, cancellationToken).ConfigureAwait(false))
            .Single();

    /// <summary>
    /// Get streaming chat message contents for the chat history provided using the specified settings.
    /// </summary>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="chatCompletionService">The target IChatCompletionService interface to extend.</param>
    /// <param name="prompt">The standardized prompt input.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    public static IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        this IChatCompletionService chatCompletionService,
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // Try to parse the text as a chat history
        if (ChatPromptParser.TryParse(prompt, out var chatHistory))
        {
            return chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        }

        //Otherwise, use the prompt as the chat system message
        return chatCompletionService.GetStreamingChatMessageContentsAsync(new ChatHistory(prompt), executionSettings, kernel, cancellationToken);
    }
}
