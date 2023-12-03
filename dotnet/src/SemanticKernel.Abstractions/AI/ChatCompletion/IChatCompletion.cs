﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.AI.ChatCompletion;

/// <summary>
/// Interface for chat completion services
/// </summary>
public interface IChatCompletion : IAIService
{
    /// <summary>
    /// Get chat multiple chat content choices for the prompt and settings.
    /// </summary>
    /// <remarks>
    /// This should be used when the settings request for more than one choice.
    /// </remarks>
    /// <param name="chat">The chat history context.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat results generated by the remote model</returns>
    Task<IReadOnlyList<ChatContent>> GetChatContentsAsync(
        ChatHistory chat,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming chat contents for the chat history provided using the specified request settings.
    /// </summary>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="chatHistory">The chat history to complete.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    IAsyncEnumerable<StreamingChatContent> GetStreamingChatContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    #region Obsolete
    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the AI service</param>
    /// <returns>Chat object</returns>
    [Obsolete("IChatCompletionV2")]
    ChatHistory CreateNewChat(string? instructions = null);

    /// <summary>
    /// Get chat completion results for the prompt and settings.
    /// </summary>
    /// <param name="chat">The chat history context.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat results generated by the remote model</returns>
    [Obsolete("IChatCompletionV2")]
    Task<IReadOnlyList<IChatResult>> GetChatCompletionsAsync(
        ChatHistory chat,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chat completion results for the prompt and settings.
    /// </summary>
    /// <param name="prompt">The standardized prompt input.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat results generated by the remote model</returns>
    [Obsolete("IChatCompletionV2")]
    Task<IReadOnlyList<IChatResult>> GetChatCompletionsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming results for the chat history provided using the specified request settings.
    /// Each modality may support for different types of streaming result.
    /// </summary>
    /// <remarks>
    /// Usage of this method may be more efficient if the connector has a dedicated API to return this result without extra allocations for StreamingResultChunk abstraction.
    /// </remarks>
    /// <exception cref="NotSupportedException">Throws if the specified type is not the same or fail to cast</exception>
    /// <param name="chatHistory">The chat history to complete.</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    [Obsolete("IChatCompletionV2")]
    IAsyncEnumerable<T> GetStreamingContentAsync<T>(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);
    #endregion
}
