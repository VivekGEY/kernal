﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Connectors.Anthropic.Core;

internal sealed class AnthropicRequest
{
    /// <summary>
    /// Input messages.<br/>
    /// Our models are trained to operate on alternating user and assistant conversational turns.
    /// When creating a new Message, you specify the prior conversational turns with the messages parameter,
    /// and the model then generates the next Message in the conversation.
    /// Each input message must be an object with a role and content. You can specify a single user-role message,
    /// or you can include multiple user and assistant messages. The first message must always use the user role.
    /// If the final message uses the assistant role, the response content will continue immediately
    /// from the content in that message. This can be used to constrain part of the model's response.
    /// </summary>
    [JsonPropertyName("messages")]
    public IList<Message> Messages { get; set; } = null!;

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<AnthropicToolFunctionDeclaration>? Tools { get; set; }

    [JsonPropertyName("model")]
    public string ModelId { get; set; } = null!;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    /// <summary>
    /// A system prompt is a way of providing context and instructions to Claude, such as specifying a particular goal or persona.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("system")]
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Custom text sequences that will cause the model to stop generating.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("stop_sequences")]
    public IList<string>? StopSequences { get; set; }

    /// <summary>
    /// Enables SSE streaming.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    /// <summary>
    /// Amount of randomness injected into the response.<br/>
    /// Defaults to 1.0. Ranges from 0.0 to 1.0. Use temperature closer to 0.0 for analytical / multiple choice, and closer to 1.0 for creative and generative tasks.<br/>
    /// Note that even with temperature of 0.0, the results will not be fully deterministic.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    /// In nucleus sampling, we compute the cumulative distribution over all the options for each subsequent token
    /// in decreasing probability order and cut it off once it reaches a particular probability specified by top_p.
    /// You should either alter temperature or top_p, but not both.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    /// <summary>
    /// Only sample from the top K options for each subsequent token.
    /// Used to remove "long tail" low probability responses.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    public void AddFunction(AnthropicFunction function)
    {
        this.Tools ??= new List<AnthropicToolFunctionDeclaration>();
        this.Tools.Add(function.ToFunctionDeclaration());
    }

    public void AddChatMessage(ChatMessageContent message)
    {
        Verify.NotNull(this.Messages);
        Verify.NotNull(message);

        this.Messages.Add(CreateClaudeMessageFromChatMessage(message));
    }

    /// <summary>
    /// Creates a <see cref="AnthropicRequest"/> object from the given <see cref="ChatHistory"/> and <see cref="AnthropicPromptExecutionSettings"/>.
    /// </summary>
    /// <param name="chatHistory">The chat history to be assigned to the <see cref="AnthropicRequest"/>.</param>
    /// <param name="executionSettings">The execution settings to be applied to the <see cref="AnthropicRequest"/>.</param>
    /// <param name="streamingMode">Enables SSE streaming. (optional)</param>
    /// <returns>A new instance of <see cref="AnthropicRequest"/>.</returns>
    internal static AnthropicRequest FromChatHistoryAndExecutionSettings(
        ChatHistory chatHistory,
        AnthropicPromptExecutionSettings executionSettings,
        bool streamingMode = false)
    {
        AnthropicRequest request = CreateRequest(chatHistory, executionSettings, streamingMode);
        AddMessages(chatHistory, request);
        return request;
    }

    private static void AddMessages(ChatHistory chatHistory, AnthropicRequest request)
        => request.Messages = chatHistory.Select(CreateClaudeMessageFromChatMessage).ToList();

    private static Message CreateClaudeMessageFromChatMessage(ChatMessageContent message)
    {
        return new Message
        {
            Role = message.Role,
            Contents = CreateClaudeMessages(message)
        };
    }

    private static AnthropicRequest CreateRequest(ChatHistory chatHistory, AnthropicPromptExecutionSettings executionSettings, bool streamingMode)
    {
        AnthropicRequest request = new()
        {
            ModelId = executionSettings.ModelId ?? throw new InvalidOperationException("Model ID must be provided."),
            MaxTokens = executionSettings.MaxTokens ?? throw new InvalidOperationException("Max tokens must be provided."),
            SystemPrompt = chatHistory.SingleOrDefault(c => c.Role == AuthorRole.System)?.Content,
            StopSequences = executionSettings.StopSequences,
            Stream = streamingMode,
            Temperature = executionSettings.Temperature,
            TopP = executionSettings.TopP,
            TopK = executionSettings.TopK
        };
        return request;
    }

    private static List<AnthropicContent> CreateClaudeMessages(ChatMessageContent content)
    {
        List<AnthropicContent> messages = new();
        switch (content)
        {
            case AnthropicChatMessageContent { CalledToolResult: not null } contentWithCalledTool:
                messages.Add(new AnthropicToolResultContent
                {
                    ToolId = contentWithCalledTool.CalledToolResult.ToolUseId ?? throw new InvalidOperationException("Tool ID must be provided."),
                    Content = new AnthropicTextContent(contentWithCalledTool.CalledToolResult.FunctionResult.ToString())
                });
                break;
            case AnthropicChatMessageContent { ToolCalls: not null } contentWithToolCalls:
                messages.AddRange(contentWithToolCalls.ToolCalls.Select(toolCall =>
                    new AnthropicToolCallContent
                    {
                        ToolId = toolCall.ToolUseId,
                        FunctionName = toolCall.FullyQualifiedName,
                        Arguments = JsonSerializer.SerializeToNode(toolCall.Arguments),
                    }));
                break;
            default:
                messages.AddRange(content.Items.Select(GetClaudeMessageFromKernelContent));
                break;
        }

        if (messages.Count == 0)
        {
            messages.Add(new AnthropicTextContent(content.Content ?? string.Empty));
        }

        return messages;
    }

    private static AnthropicContent GetClaudeMessageFromKernelContent(KernelContent content) => content switch
    {
        TextContent textContent => new AnthropicTextContent(textContent.Text ?? string.Empty),
        ImageContent imageContent => new AnthropicImageContent(
            type: "base64",
            mediaType: imageContent.MimeType ?? throw new InvalidOperationException("Image content must have a MIME type."),
            data: imageContent.Data.HasValue
                ? Convert.ToBase64String(imageContent.Data.Value.ToArray())
                : throw new InvalidOperationException("Image content must have a data.")
        ),
        _ => throw new NotSupportedException($"Content type '{content.GetType().Name}' is not supported.")
    };

    internal sealed class Message
    {
        [JsonConverter(typeof(AuthorRoleConverter))]
        [JsonPropertyName("role")]
        public AuthorRole Role { get; set; }

        [JsonPropertyName("content")]
        public IList<AnthropicContent> Contents { get; set; } = null!;
    }
}
