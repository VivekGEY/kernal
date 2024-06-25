﻿// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.SemanticKernel.Agents;

/// <summary>
/// Contract for an agent that utilizes a <see cref="ChatHistoryChannel"/>.
/// </summary>
public interface IChatHistoryHandler
{
    /// <summary>
    /// Entry point for calling into an agent from a <see cref="ChatHistoryChannel"/>.
    /// </summary>
    /// <param name="history">The chat history at the point the channel is created.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Asynchronous enumeration of messages.</returns>
    IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Entry point for calling into an agent from a <see cref="ChatHistoryChannel"/> for streaming content.
    /// </summary>
    /// <param name="history">The chat history at the point the channel is created.</param>
    /// <param name="logger">The logger associated with the <see cref="ChatHistoryChannel"/></param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Asynchronous enumeration of streaming content.</returns>
    public abstract IAsyncEnumerable<StreamingChatMessageContent> InvokeStreamingAsync(
        IReadOnlyList<ChatMessageContent> history,
        ILogger logger,
        CancellationToken cancellationToken = default);
}
