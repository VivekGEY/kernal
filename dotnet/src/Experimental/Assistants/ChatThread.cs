﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Experimental.Assistants.Extensions;
using Microsoft.SemanticKernel.Experimental.Assistants.Models;

namespace Microsoft.SemanticKernel.Experimental.Assistants;

/// <summary>
/// Represents a thread that contains messages.
/// </summary>
public sealed class ChatThread : IChatThread
{
    /// <inheritdoc/>
    public string Id { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<ChatMessage> Messages => this._messages.AsReadOnly();

    private readonly IOpenAIRestContext _restContext;
    private readonly List<ChatMessage> _messages;
    private readonly Dictionary<string, ChatMessage> _messageIndex;

    /// <summary>
    /// Create a new thread.
    /// </summary>
    /// <param name="restContext">An context for accessing OpenAI REST endpoint</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>An initialized <see cref="ChatThread"> instance.</see></returns>
    public static async Task<ChatThread> CreateAsync(IOpenAIRestContext restContext, CancellationToken cancellationToken = default)
    {
        var threadModel =
            await restContext.CreateThreadAsync(cancellationToken).ConfigureAwait(false) ??
            throw new SKException("Unexpected failure creating thread: no result.");

        return new ChatThread(threadModel, messageListModel: null, restContext);
    }

    /// <summary>
    /// Retrieve an existing thread.
    /// </summary>
    /// <param name="restContext">An context for accessing OpenAI REST endpoint</param>
    /// <param name="threadId">The thread identifier</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>An initialized <see cref="ChatThread"> instance.</see></returns>
    public static async Task<ChatThread> GetAsync(IOpenAIRestContext restContext, string threadId, CancellationToken cancellationToken = default)
    {
        var threadModel =
            await restContext.GetThreadAsync(threadId, cancellationToken).ConfigureAwait(false) ??
            throw new SKException($"Unexpected failure retrieving thread: no result. ({threadId})");

        var messageListModel =
            await restContext.GetMessagesAsync(threadId, cancellationToken).ConfigureAwait(false) ??
            throw new SKException($"Unexpected failure retrieving thread: no result. ({threadId})");

        return new ChatThread(threadModel, messageListModel, restContext);
    }

    /// <inheritdoc/>
    public async Task<ChatMessage> AddUserMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var messageModel =
            await this._restContext.CreateUserTextMessageAsync(
                this.Id,
                message,
                cancellationToken).ConfigureAwait(false);

        return this.AddMessage(messageModel);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ChatMessage>> InvokeAsync(string assistantId, string? instructions, CancellationToken cancellationToken)
    {
        var runModel = await this._restContext.CreateRunAsync(this.Id, assistantId, instructions, cancellationToken).ConfigureAwait(false);

        for (var index = this._messages.Count - 1; index >= 0; --index) // $$$ HAXX
        {
            var message = this._messages[index];
            if (message.AssistantId == assistantId)
            {
                return
                    new[]
                    {
                        message,
                    };
            }
        }

        return Array.Empty<ChatMessage>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessage"/> class.
    /// </summary>
    private ChatThread(
        ThreadModel threadModel,
        ThreadRunStepListModel? messageListModel,
        IOpenAIRestContext restContext)
    {
        this.Id = threadModel.Id;
        this._messages = (messageListModel?.Data ?? new List<ThreadMessageModel>()).Select(m => new ChatMessage(m)).ToList();
        this._messageIndex = this._messages.ToDictionary(m => m.Id);
        this._restContext = restContext;
    }

    private ChatMessage AddMessage(ThreadMessageModel messageModel)
    {
        var message = new ChatMessage(messageModel);

        this._messages.Add(message);
        this._messageIndex.Add(message.Id, message);

        return message;
    }
}
