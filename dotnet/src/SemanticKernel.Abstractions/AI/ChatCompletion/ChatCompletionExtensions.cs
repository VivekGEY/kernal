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
    /// <param name="cancellationToken">The asynchronous cancellation token (optional).</param>
    /// <remarks>This extension does not support multiple prompt results (only the first will be returned).</remarks>
    /// <returns>A task representing the generated chat message in string format.</returns>
    public static async Task<string> GenerateMessageAsync(
        this IChatCompletion chatCompletion,
        ChatHistory chat,
        PromptExecutionSettings? executionSettings = null,
        CancellationToken cancellationToken = default)
    {
        // Using var below results in Microsoft.CSharp.RuntimeBinder.RuntimeBinderException : Cannot apply indexing with [] to an expression of type 'object'
        IReadOnlyList<IChatResult> chatResults = await chatCompletion.GetChatCompletionsAsync(chat, executionSettings, cancellationToken).ConfigureAwait(false);
        var firstChatMessage = await chatResults[0].GetChatMessageAsync(cancellationToken).ConfigureAwait(false);
        return firstChatMessage.Content;
    }

    /// <summary>
    /// Get asynchronous stream of <see cref="StreamingContent"/>.
    /// </summary>
    /// <param name="chatCompletion">Chat completion target</param>
    /// <param name="input">The input string that will be used as the instructions of the chat</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming result updates generated by the remote model</returns>
    public static IAsyncEnumerable<StreamingContent> GetStreamingContentAsync(
        this IChatCompletion chatCompletion,
        string input,
        PromptExecutionSettings? executionSettings = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(input);
        return chatCompletion.GetStreamingContentAsync<StreamingContent>(chatCompletion.CreateNewChat(input), executionSettings, cancellationToken);
    }

    /// <summary>
    /// Get asynchronous stream of <see cref="StreamingContent"/>.
    /// </summary>
    /// <param name="chatCompletion">Chat completion target</param>
    /// <param name="input">The input string that will be used as the instructions of the chat</param>
    /// <param name="executionSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming result updates generated by the remote model</returns>
    public static IAsyncEnumerable<T> GetStreamingContentAsync<T>(
        this IChatCompletion chatCompletion,
        string input,
        PromptExecutionSettings? executionSettings = null,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(input);
        return chatCompletion.GetStreamingContentAsync<T>(chatCompletion.CreateNewChat(input), executionSettings, cancellationToken);
    }
}
