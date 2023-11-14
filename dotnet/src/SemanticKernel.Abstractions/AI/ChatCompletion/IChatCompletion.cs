﻿// Copyright (c) Microsoft. All rights reserved.

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
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the AI service</param>
    /// <returns>Chat object</returns>
    ChatHistory CreateNewChat(string? instructions = null);

    /// <summary>
    /// Get chat completion results for the prompt and settings.
    /// </summary>
    /// <param name="chat">The chat history context.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different chat results generated by the remote model</returns>
    Task<IReadOnlyList<IChatResult>> GetChatCompletionsAsync(
        ChatHistory chat,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chat streaming completion results for the prompt and settings.
    /// </summary>
    /// <param name="chat">The chat history context.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>AsyncEnumerable list of different streaming chat results generated by the remote model</returns>
    IAsyncEnumerable<IChatStreamingResult> GetStreamingChatCompletionsAsync(
        ChatHistory chat,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming completion results for the prompt and settings.
    /// </summary>
    /// <param name="input">The input string. (May be a JSON for complex objects, Byte64 for binary, will depend on the connector spec).</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming result updates generated by the remote model</returns>
    IAsyncEnumerable<StreamingResultUpdate> GetStreamingUpdatesAsync(
        string input,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming results for the prompt and settings.
    /// </summary>
    /// <param name="input">The input string. (May be a JSON for complex objects, Byte64 for binary, will depend on the connector spec).</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming string updates generated by the remote model</returns>
    IAsyncEnumerable<string> GetStringStreamingUpdatesAsync(
        string input,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get streaming results for the prompt and settings.
    /// </summary>
    /// <param name="input">The input string. (May be a JSON for complex objects, Byte64 for binary, will depend on the connector spec).</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Streaming list of different completion streaming byte array updates generated by the remote model</returns>
    IAsyncEnumerable<byte[]> GetByteStreamingUpdatesAsync(
        string input,
        AIRequestSettings? requestSettings = null,
        CancellationToken cancellationToken = default);
}
