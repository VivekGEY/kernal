﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Text;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;

public abstract class ClientBase
{
    /// <summary>
    /// Model Id or Deployment Name
    /// </summary>
    protected string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI / Azure OpenAI Client
    /// </summary>
    protected abstract OpenAIClient Client { get; }

    /// <summary>
    /// Creates a completion for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Text generated by the remote model</returns>
    protected async Task<string> InternalCompleteTextAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(requestSettings);

        ValidateMaxTokens(requestSettings.MaxTokens);
        var options = this.CreateCompletionsOptions(text, requestSettings);

        Response<Completions>? response = await RunRequestAsync<Response<Completions>?>(
            () => this.Client.GetCompletionsAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

        if (response == null || response.Value.Choices.Count < 1)
        {
            throw new AIException(AIException.ErrorCodes.InvalidResponseContent, "Text completions not found");
        }

        return response.Value.Choices[0].Text;
    }

    /// <summary>
    /// Creates a completion stream for the pormpt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Stream the text generated by the remote model</returns>
    protected async IAsyncEnumerable<string> InternalCompletionStreamAsync(
        string text,
        CompleteRequestSettings requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(requestSettings);

        ValidateMaxTokens(requestSettings.MaxTokens);
        var options = this.CreateCompletionsOptions(text, requestSettings);

        Response<StreamingCompletions>? response = await RunRequestAsync<Response<StreamingCompletions>>(
            () => this.Client.GetCompletionsStreamingAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

        using StreamingCompletions streamingChatCompletions = response.Value;
        await foreach (StreamingChoice choice in streamingChatCompletions.GetChoicesStreaming(cancellationToken))
        {
            await foreach (string message in choice.GetTextStreaming(cancellationToken))
            {
                yield return message;
            }

            yield return Environment.NewLine;
        }
    }

    /// <summary>
    /// Generates an embedding from the given <paramref name="data"/>.
    /// </summary>
    /// <param name="data">List of strings to generate embeddings for</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of embeddings</returns>
    protected async Task<IList<Embedding<float>>> InternalGenerateTextEmbeddingsAsync(
        IList<string> data,
        CancellationToken cancellationToken = default)
    {
        var result = new List<Embedding<float>>();
        foreach (string text in data)
        {
            var options = new EmbeddingsOptions(text);

            Response<Embeddings>? response = await RunRequestAsync<Response<Embeddings>?>(
                () => this.Client.GetEmbeddingsAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

            if (response == null || response.Value.Data.Count < 1)
            {
                throw new AIException(AIException.ErrorCodes.InvalidResponseContent, "Text embedding not found");
            }

            EmbeddingItem x = response.Value.Data[0];

            result.Add(new Embedding<float>(x.Embedding));
        }

        return result;
    }

    /// <summary>
    /// Generate a new chat message
    /// </summary>
    /// <param name="chat">Chat history</param>
    /// <param name="requestSettings">AI request settings</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>Generated chat message in string format</returns>
    protected async Task<string> InternalGenerateChatMessageAsync(
        ChatHistory chat,
        ChatRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);
        Verify.NotNull(requestSettings);

        if (requestSettings.MaxTokens < 1)
        {
            throw new AIException(
                AIException.ErrorCodes.InvalidRequest,
                $"MaxTokens {requestSettings.MaxTokens} is not valid, the value must be greater than zero");
        }

        var options = new ChatCompletionsOptions
        {
            MaxTokens = requestSettings.MaxTokens,
            Temperature = (float?)requestSettings.Temperature,
            NucleusSamplingFactor = (float?)requestSettings.TopP,
            FrequencyPenalty = (float?)requestSettings.FrequencyPenalty,
            PresencePenalty = (float?)requestSettings.PresencePenalty,
            ChoicesPerPrompt = 1,
        };

        if (requestSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in requestSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        foreach (ChatHistory.Message message in chat.Messages)
        {
            var role = message.AuthorRole switch
            {
                ChatHistory.AuthorRoles.User => ChatRole.User,
                ChatHistory.AuthorRoles.Assistant => ChatRole.Assistant,
                ChatHistory.AuthorRoles.System => ChatRole.System,
                _ => throw new ArgumentException($"Invalid chat message author: {message.AuthorRole:G}")
            };

            options.Messages.Add(new ChatMessage(role, message.Content));
        }

        Response<ChatCompletions>? response = await RunRequestAsync<Response<ChatCompletions>?>(
            () => this.Client.GetChatCompletionsAsync(this.ModelId, options, cancellationToken)).ConfigureAwait(false);

        if (response == null || response.Value.Choices.Count < 1)
        {
            throw new AIException(AIException.ErrorCodes.InvalidResponseContent, "Chat completions not found");
        }

        return response.Value.Choices[0].Message.Content;
    }

    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="instructions">Optional chat instructions for the AI service</param>
    /// <returns>Chat object</returns>
    protected ChatHistory InternalCreateNewChat(string instructions = "")
    {
        return new OpenAIChatHistory(instructions);
    }

    /// <summary>
    /// Creates a completion for the prompt and settings using the chat endpoint
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="requestSettings">Request settings for the completion API</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Text generated by the remote model</returns>
    protected async Task<string> InternalCompleteTextUsingChatAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        var chat = this.InternalCreateNewChat();
        chat.AddMessage(ChatHistory.AuthorRoles.User, text);
        var settings = new ChatRequestSettings
        {
            MaxTokens = requestSettings.MaxTokens,
            Temperature = requestSettings.Temperature,
            TopP = requestSettings.TopP,
            PresencePenalty = requestSettings.PresencePenalty,
            FrequencyPenalty = requestSettings.FrequencyPenalty,
            StopSequences = requestSettings.StopSequences,
        };

        return await this.InternalGenerateChatMessageAsync(chat, settings, cancellationToken).ConfigureAwait(false);
    }

    private CompletionsOptions CreateCompletionsOptions(string text, CompleteRequestSettings requestSettings)
    {
        var options = new CompletionsOptions
        {
            Prompts = { text.NormalizeLineEndings() },
            MaxTokens = requestSettings.MaxTokens,
            Temperature = (float?)requestSettings.Temperature,
            NucleusSamplingFactor = (float?)requestSettings.TopP,
            FrequencyPenalty = (float?)requestSettings.FrequencyPenalty,
            PresencePenalty = (float?)requestSettings.PresencePenalty,
            Echo = false,
            ChoicesPerPrompt = 1,
            GenerationSampleCount = 1,
            LogProbabilityCount = null,
            User = null,
        };

        if (requestSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in requestSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        return options;
    }

    private static void ValidateMaxTokens(int maxTokens)
    {
        if (maxTokens < 1)
        {
            throw new AIException(
                AIException.ErrorCodes.InvalidRequest,
                $"MaxTokens {maxTokens} is not valid, the value must be greater than zero");
        }
    }

    private static async Task<T> RunRequestAsync<T>(Func<Task<T>> request)
    {
        try
        {
            return await request.Invoke().ConfigureAwait(false);
        }
        catch (RequestFailedException e)
        {
            switch (e.Status)
            {
                case (int)HttpStatusCodeType.BadRequest:
                case (int)HttpStatusCodeType.MethodNotAllowed:
                case (int)HttpStatusCodeType.NotFound:
                case (int)HttpStatusCodeType.NotAcceptable:
                case (int)HttpStatusCodeType.Conflict:
                case (int)HttpStatusCodeType.Gone:
                case (int)HttpStatusCodeType.LengthRequired:
                case (int)HttpStatusCodeType.PreconditionFailed:
                case (int)HttpStatusCodeType.RequestEntityTooLarge:
                case (int)HttpStatusCodeType.RequestUriTooLong:
                case (int)HttpStatusCodeType.UnsupportedMediaType:
                case (int)HttpStatusCodeType.RequestedRangeNotSatisfiable:
                case (int)HttpStatusCodeType.ExpectationFailed:
                case (int)HttpStatusCodeType.HttpVersionNotSupported:
                case (int)HttpStatusCodeType.UpgradeRequired:
                case (int)HttpStatusCodeType.MisdirectedRequest:
                case (int)HttpStatusCodeType.UnprocessableEntity:
                case (int)HttpStatusCodeType.Locked:
                case (int)HttpStatusCodeType.FailedDependency:
                case (int)HttpStatusCodeType.PreconditionRequired:
                case (int)HttpStatusCodeType.RequestHeaderFieldsTooLarge:
                    throw new AIException(
                        AIException.ErrorCodes.InvalidRequest,
                        $"The request is not valid, HTTP status: {e.Status}",
                        e.Message);

                case (int)HttpStatusCodeType.Unauthorized:
                case (int)HttpStatusCodeType.Forbidden:
                case (int)HttpStatusCodeType.ProxyAuthenticationRequired:
                case (int)HttpStatusCodeType.UnavailableForLegalReasons:
                case (int)HttpStatusCodeType.NetworkAuthenticationRequired:
                    throw new AIException(
                        AIException.ErrorCodes.AccessDenied,
                        $"The request is not authorized, HTTP status: {e.Status}",
                        e.Message);

                case (int)HttpStatusCodeType.RequestTimeout:
                    throw new AIException(
                        AIException.ErrorCodes.RequestTimeout,
                        $"The request timed out, HTTP status: {e.Status}");

                case (int)HttpStatusCodeType.TooManyRequests:
                    throw new AIException(
                        AIException.ErrorCodes.Throttling,
                        $"Too many requests, HTTP status: {e.Status}",
                        e.Message);

                case (int)HttpStatusCodeType.InternalServerError:
                case (int)HttpStatusCodeType.NotImplemented:
                case (int)HttpStatusCodeType.BadGateway:
                case (int)HttpStatusCodeType.ServiceUnavailable:
                case (int)HttpStatusCodeType.GatewayTimeout:
                case (int)HttpStatusCodeType.InsufficientStorage:
                    throw new AIException(
                        AIException.ErrorCodes.ServiceError,
                        $"The service failed to process the request, HTTP status:{e.Status}",
                        e.Message);

                default:
                    throw new AIException(
                        AIException.ErrorCodes.UnknownError,
                        $"Unexpected HTTP response, status: {e.Status}",
                        e.Message);
            }
        }
        catch (Exception e) when (e is not AIException)
        {
            throw new AIException(
                AIException.ErrorCodes.UnknownError,
                $"Something went wrong: {e.Message}", e);
        }
    }
}
