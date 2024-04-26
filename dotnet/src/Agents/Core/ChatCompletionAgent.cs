﻿// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents;

/// <summary>
/// A <see cref="KernelAgent"/> specialization based on <see cref="IChatCompletionService"/>.
/// </summary>
/// <remarks>
/// NOTE: Enable OpenAIPromptExecutionSettings.ToolCallBehavior for agent plugins.
/// (<see cref="ChatCompletionAgent.ExecutionSettings"/>)
/// </remarks>
public sealed class ChatCompletionAgent : ChatHistoryKernelAgent
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatCompletionAgent"/> class.
    /// </summary>
    public ChatCompletionAgent()
    {
        this._logger = this.Kernel.LoggerFactory.CreateLogger<ChatCompletionAgent>();
    }

    /// <summary>
    /// Optional execution settings for the agent.
    /// </summary>
    public PromptExecutionSettings? ExecutionSettings { get; set; }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        IReadOnlyList<ChatMessageContent> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // %%% TAO - CONSIDER THIS SECTION FOR LOGGING
        this._logger.LogDebug("Agent {AgentId} invoked.", this.Id);

        var chatCompletionService = this.Kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory chat = [];
        if (!string.IsNullOrWhiteSpace(this.Instructions))
        {
            chat.Add(new ChatMessageContent(AuthorRole.System, this.Instructions) { AuthorName = this.Name });
        }
        chat.AddRange(history);

        var messages =
            await chatCompletionService.GetChatMessageContentsAsync(
                chat,
                this.ExecutionSettings,
                this.Kernel,
                cancellationToken).ConfigureAwait(false);

        foreach (var message in messages ?? [])
        {
            // TODO: MESSAGE SOURCE - ISSUE #5731
            message.AuthorName = this.Name;

            yield return message;
        }
    }
}
