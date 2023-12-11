﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.TextGeneration;
using Microsoft.SemanticKernel.TextToImage;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

// TODO: forward ETW logging to ILogger, see https://learn.microsoft.com/en-us/dotnet/azure/sdk/logging

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with OpenAI services.
/// </summary>
internal abstract class ClientCore
{
    private const int MaxResultsPerPrompt = 128;

    internal ClientCore(ILogger? logger = null)
    {
        this.Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Model Id or Deployment Name
    /// </summary>
    internal string DeploymentOrModelName { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI / Azure OpenAI Client
    /// </summary>
    internal abstract OpenAIClient Client { get; }

    /// <summary>
    /// Logger instance
    /// </summary>
    internal ILogger Logger { get; set; }

    /// <summary>
    /// Storage for AI service attributes.
    /// </summary>
    internal Dictionary<string, object?> Attributes { get; } = new();

    /// <summary>
    /// Instance of <see cref="Meter"/> for metrics.
    /// </summary>
    private static readonly Meter s_meter = new("Microsoft.SemanticKernel.Connectors.OpenAI");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of prompt tokens used.
    /// </summary>
    private static readonly Counter<int> s_promptTokensCounter =
        s_meter.CreateCounter<int>(
            name: "semantic_kernel.connectors.openai.tokens.prompt",
            unit: "{token}",
            description: "Number of prompt tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the number of completion tokens used.
    /// </summary>
    private static readonly Counter<int> s_completionTokensCounter =
        s_meter.CreateCounter<int>(
            name: "semantic_kernel.connectors.openai.tokens.completion",
            unit: "{token}",
            description: "Number of completion tokens used");

    /// <summary>
    /// Instance of <see cref="Counter{T}"/> to keep track of the total number of tokens used.
    /// </summary>
    private static readonly Counter<int> s_totalTokensCounter =
        s_meter.CreateCounter<int>(
            name: "semantic_kernel.connectors.openai.tokens.total",
            unit: "{token}",
            description: "Number of tokens used");

    /// <summary>
    /// Creates completions for the prompt and settings.
    /// </summary>
    /// <param name="text">The prompt to complete.</param>
    /// <param name="executionSettings">Execution settings for the completion API.</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Completions generated by the remote model</returns>
    internal async Task<IReadOnlyList<TextContent>> GetTextResultsAsync(
        string text,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings textExecutionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings, OpenAIPromptExecutionSettings.DefaultTextMaxTokens);

        ValidateMaxTokens(textExecutionSettings.MaxTokens);

        var options = CreateCompletionsOptions(text, textExecutionSettings, this.DeploymentOrModelName);

        var responseData = (await RunRequestAsync(() => this.Client.GetCompletionsAsync(options, cancellationToken)).ConfigureAwait(false)).Value;
        if (responseData.Choices.Count == 0)
        {
            throw new KernelException("Text completions not found");
        }

        this.CaptureUsageDetails(responseData.Usage);
        var metadata = GetResponseMetadata(responseData);
        return responseData.Choices.Select(choice => new TextContent(choice.Text, this.DeploymentOrModelName, choice, Encoding.UTF8, new Dictionary<string, object?>(metadata))).ToList();
    }

    internal async IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings textExecutionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings, OpenAIPromptExecutionSettings.DefaultTextMaxTokens);

        ValidateMaxTokens(textExecutionSettings.MaxTokens);

        var options = CreateCompletionsOptions(prompt, textExecutionSettings, this.DeploymentOrModelName);

        StreamingResponse<Completions>? response = await RunRequestAsync(() => this.Client.GetCompletionsStreamingAsync(options, cancellationToken)).ConfigureAwait(false);

        Dictionary<string, object?>? metadata = null;
        await foreach (Completions completions in response)
        {
            metadata ??= GetResponseMetadata(completions);
            foreach (Choice choice in completions.Choices)
            {
                yield return new OpenAIStreamingTextContent(choice.Text, choice.Index, this.DeploymentOrModelName, choice, new(metadata));
            }
        }
    }

    private static Dictionary<string, object?> GetResponseMetadata(Completions completions)
    {
        return new Dictionary<string, object?>(4)
        {
            { nameof(completions.Id), completions.Id },
            { nameof(completions.Created), completions.Created },
            { nameof(completions.PromptFilterResults), completions.PromptFilterResults },
            { nameof(completions.Usage), completions.Usage },
        };
    }

    private static Dictionary<string, object?> GetResponseMetadata(ChatCompletions completions)
    {
        return new Dictionary<string, object?>(4)
        {
            { nameof(completions.Id), completions.Id },
            { nameof(completions.Created), completions.Created },
            { nameof(completions.PromptFilterResults), completions.PromptFilterResults },
            { nameof(completions.Usage), completions.Usage },
        };
    }

    private static Dictionary<string, object?> GetResponseMetadata(StreamingChatCompletionsUpdate completions)
    {
        return new Dictionary<string, object?>(2)
        {
            { nameof(completions.Id), completions.Id },
            { nameof(completions.Created), completions.Created },
        };
    }

    /// <summary>
    /// Generates an embedding from the given <paramref name="data"/>.
    /// </summary>
    /// <param name="data">List of strings to generate embeddings for</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of embeddings</returns>
    internal async Task<IList<ReadOnlyMemory<float>>> GetEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel,
        CancellationToken cancellationToken)
    {
        var result = new List<ReadOnlyMemory<float>>(data.Count);
        foreach (string text in data)
        {
            var options = new EmbeddingsOptions(this.DeploymentOrModelName, new[] { text });

            Response<Azure.AI.OpenAI.Embeddings> response = await RunRequestAsync(() => this.Client.GetEmbeddingsAsync(options, cancellationToken)).ConfigureAwait(false);
            if (response.Value.Data.Count == 0)
            {
                throw new KernelException("Text embedding not found");
            }

            result.Add(response.Value.Data[0].Embedding.ToArray());
        }

        return result;
    }

    /// <summary>
    /// Generate a new chat message
    /// </summary>
    /// <param name="chat">Chat history</param>
    /// <param name="executionSettings">Execution settings for the completion API.</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>Generated chat message in string format</returns>
    internal async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chat,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);

        // Convert the incoming execution settings to OpenAI settings.
        OpenAIPromptExecutionSettings chatExecutionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
        bool autoInvoke = chatExecutionSettings.FunctionCallBehavior?.AutoInvoke == true && kernel is not null;
        ValidateMaxTokens(chatExecutionSettings.MaxTokens);
        ValidateAutoInvoke(autoInvoke, chatExecutionSettings.ResultsPerPrompt);

        // Create the Azure SDK ChatCompletionOptions instance from all available information.
        var chatOptions = CreateChatCompletionsOptions(chatExecutionSettings, chat, kernel, this.DeploymentOrModelName);

        while (true)
        {
            // Make the request.
            var responseData = (await RunRequestAsync(() => this.Client.GetChatCompletionsAsync(chatOptions, cancellationToken)).ConfigureAwait(false)).Value;
            this.CaptureUsageDetails(responseData.Usage);
            if (responseData.Choices.Count == 0)
            {
                throw new KernelException("Chat completions not found");
            }

            var metadata = GetResponseMetadata(responseData);

            // If we don't want to attempt to invoke any functions, just return the result.
            // Or if we are auto-invoking but we somehow end up with other than 1 choice even though only 1 was requested, similarly bail.
            if (!autoInvoke || responseData.Choices.Count != 1)
            {
                return responseData.Choices.Select(chatChoice => new OpenAIChatMessageContent(chatChoice.Message, this.DeploymentOrModelName, new(metadata))).ToList();
            }

            // Get our single result and extract the function call information. If this isn't a function call, or if it is
            // but we're unable to find the function or extract the relevant information, just return the single result.
            ChatChoice resultChoice = responseData.Choices[0];
            OpenAIChatMessageContent result = new(resultChoice.Message, this.DeploymentOrModelName, metadata);
            OpenAIFunctionResponse? functionCallResponse = null;
            try
            {
                functionCallResponse = result.GetOpenAIFunctionResponse();
            }
            catch (JsonException e) when (resultChoice.Message.FunctionCall is not null)
            {
                if (this.Logger.IsEnabled(LogLevel.Error))
                {
                    this.Logger.LogError(e, "Failed to parse function call response for '{FunctionName}'", resultChoice.Message.FunctionCall.Name);
                }
                if (this.Logger.IsEnabled(LogLevel.Trace))
                {
                    this.Logger.LogTrace("Invalid function call arguments: '{FunctionArguments}'", resultChoice.Message.FunctionCall.Arguments);
                }
            }

            if (functionCallResponse is null ||
                !kernel!.Plugins.TryGetFunctionAndArguments(functionCallResponse, out KernelFunction? function, out KernelArguments? functionArgs))
            {
                return new[] { result };
            }

            // Otherwise, invoke the function.
            var functionResult = (await function.InvokeAsync(kernel, functionArgs, cancellationToken: cancellationToken).ConfigureAwait(false))
                .GetValue<object>() ?? string.Empty;

            var serializedFunctionResult = JsonSerializer.Serialize(functionResult);

            // Then add the relevant messages both to the chat options and to the chat history.
            // The messages are added to the chat history, even though it's not strictly required, so that the additional
            // context is available for future use by the LLM. If the caller doesn't want them, they can remove them,
            // e.g. by storing the chat history's count prior to the call and then removing back to that after the call.
            if (resultChoice.Message.Content is { Length: > 0 })
            {
                chatOptions.Messages.Add(GetRequestMessage(resultChoice.Message));
                chat.AddMessage(result);
            }

            string fullyQualifiedName = functionCallResponse.FullyQualifiedName;
            chatOptions.Messages.Add(new ChatRequestFunctionMessage(fullyQualifiedName, serializedFunctionResult));
            chat.AddFunctionMessage(serializedFunctionResult, fullyQualifiedName);

            // Most function call behaviors are optional for the service. However, if the caller has specified a required function,
            // it's not optional for the service: it needs to invoke it. And as such, if we leave it on the settings, we'll loop
            // forever, because on each call the service will be required to re-request that same invocation. We thus clear out
            // the chat options' function call and functions, so that the service doesn't see them and doesn't invoke them.
            if (chatExecutionSettings.FunctionCallBehavior is FunctionCallBehavior.RequiredFunction)
            {
                chatOptions.FunctionCall = FunctionDefinition.None;

                // Setting null or empty in this as is causing Bad Request (functions too short) or NullPointer Exception in Azure SDK
                //chatOptions.Functions = Array.Empty<FunctionDefinition>();

                // Workaround for Null Pointer Exception in Azure SDK
                chatOptions.Functions = NoFunctionToCall();
            }
        }
    }

    internal async IAsyncEnumerable<OpenAIStreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chat,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(chat);

        OpenAIPromptExecutionSettings chatExecutionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);

        ValidateMaxTokens(chatExecutionSettings.MaxTokens);

        bool autoInvoke = chatExecutionSettings.FunctionCallBehavior?.AutoInvoke == true && kernel is not null;
        ValidateAutoInvoke(autoInvoke, chatExecutionSettings.ResultsPerPrompt);

        var chatOptions = CreateChatCompletionsOptions(chatExecutionSettings, chat, kernel, this.DeploymentOrModelName);

        while (true)
        {
            // Make the request.
            var response = await RunRequestAsync(() => this.Client.GetChatCompletionsStreamingAsync(chatOptions, cancellationToken)).ConfigureAwait(false);

            // Stream any response 
            Dictionary<string, object?>? metadata = null;
            StringBuilder? contentBuilder = null;
            string? functionName = null;
            StringBuilder? functionArgumentsBuilder = null;
            ChatRole? streamedRole = default;
            CompletionsFinishReason finishReason = default;
            await foreach (StreamingChatCompletionsUpdate update in response.ConfigureAwait(false))
            {
                metadata ??= GetResponseMetadata(update);
                streamedRole ??= update.Role;
                finishReason = update.FinishReason ?? default;

                // If we're intending to invoke function calls, we need to consume that function call information.
                if (autoInvoke)
                {
                    functionName ??= update.FunctionName;

                    if (update.FunctionArgumentsUpdate is string funcArgs)
                    {
                        (functionArgumentsBuilder ??= new()).Append(funcArgs);
                    }

                    if (update.ContentUpdate is string content)
                    {
                        (contentBuilder ??= new()).Append(content);
                    }

                    if (functionName is not null)
                    {
                        // Once we start receiving function information, stop yielding the update information.
                        continue;
                    }
                }

                yield return new OpenAIStreamingChatMessageContent(update, update.ChoiceIndex ?? 0, this.DeploymentOrModelName, new(metadata));
            }

            // If we don't have a function call to invoke, we're done.
            if (!autoInvoke ||
                finishReason != CompletionsFinishReason.FunctionCall ||
                functionName is null)
            {
                yield break;
            }

            // Extract the function call information. If we're unable to find the function or extract the relevant information, we're done.
            Debug.Assert(autoInvoke);
            FunctionCall functionCall = new(functionName!, functionArgumentsBuilder?.ToString() ?? string.Empty);
            OpenAIFunctionResponse? functionCallResponse = null;
            try
            {
                functionCallResponse = OpenAIFunctionResponse.FromFunctionCall(functionCall);
            }
            catch (JsonException e)
            {
                if (this.Logger.IsEnabled(LogLevel.Error))
                {
                    this.Logger.LogError(e, "Failed to parse function call response for '{FunctionName}'", functionCall.Name);
                }
                if (this.Logger.IsEnabled(LogLevel.Trace))
                {
                    this.Logger.LogTrace("Invalid function call arguments: '{FunctionArguments}'", functionCall.Arguments);
                }
            }

            if (functionCallResponse is null ||
                !kernel!.Plugins.TryGetFunctionAndArguments(functionCallResponse, out KernelFunction? function, out KernelArguments? functionArgs))
            {
                yield break;
            }

            // Otherwise, invoke the function.
            var functionResult = (await function.InvokeAsync(kernel, functionArgs, cancellationToken: cancellationToken).ConfigureAwait(false))
                .GetValue<object>() ?? string.Empty;

            var serializedFunctionResult = JsonSerializer.Serialize(functionResult);

            // Then add the relevant messages both to the chat options and to the chat history.
            // The messages are added to the chat history, even though it's not strictly required, so that the additional
            // context is available for future use by the LLM. If the caller doesn't want them, they can remove them,
            // e.g. by storing the chat history's count prior to the call and then removing back to that after the call.
            string contents = contentBuilder?.ToString() ?? string.Empty;
            string fqn = functionCallResponse.FullyQualifiedName;

            if (contents.Length > 0)
            {
                chatOptions.Messages.Add(GetRequestMessage(streamedRole ?? default, contents, functionCall));
                chat.AddAssistantMessage(contents, functionCall);
            }

            chatOptions.Messages.Add(new ChatRequestFunctionMessage(fqn, serializedFunctionResult));
            chat.AddFunctionMessage(serializedFunctionResult, fqn);

            // Most function call behaviors are optional for the service. However, if the caller has specified a required function,
            // it's not optional for the service: it needs to invoke it. And as such, if we leave it on the settings, we'll loop
            // forever, because on each call the service will be required to re-request that same invocation. We thus clear out
            // the chat options' function call and functions, so that the service doesn't see them and doesn't invoke them.
            if (chatExecutionSettings.FunctionCallBehavior is FunctionCallBehavior.RequiredFunction)
            {
                chatOptions.FunctionCall = FunctionDefinition.None;

                // Setting null or empty in this as is causing Bad Request (functions too short) or NullPointer Exception in Azure SDK
                //chatOptions.Functions = Array.Empty<FunctionDefinition>();

                // Workaround for Null Pointer Exception in Azure SDK
                chatOptions.Functions = NoFunctionToCall();
            }
        }
    }

    internal async IAsyncEnumerable<StreamingTextContent> GetChatAsTextStreamingContentsAsync(
        string prompt,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings chatSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
        ChatHistory chat = CreateNewChat(prompt, chatSettings);

        await foreach (var chatUpdate in this.GetStreamingChatMessageContentsAsync(chat, executionSettings, kernel, cancellationToken))
        {
            yield return new StreamingTextContent(chatUpdate.Content, chatUpdate.ChoiceIndex, chatUpdate.ModelId, chatUpdate, Encoding.UTF8, chatUpdate.Metadata);
        }
    }

    internal async Task<IReadOnlyList<TextContent>> GetChatAsTextContentsAsync(
        string text,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings chatSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(executionSettings);

        ChatHistory chat = CreateNewChat(text, chatSettings);
        return (await this.GetChatMessageContentsAsync(chat, chatSettings, kernel, cancellationToken).ConfigureAwait(false))
            .Select(chat => new TextContent(chat.Content, chat.ModelId, chat.Content, Encoding.UTF8, chat.Metadata))
            .ToList();
    }

    internal void AddAttribute(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            this.Attributes.Add(key, value!);
        }
    }

    /// <summary>Gets options to use for an OpenAIClient</summary>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <returns>An instance of <see cref="OpenAIClientOptions"/>.</returns>
    internal static OpenAIClientOptions GetOpenAIClientOptions(HttpClient? httpClient)
    {
        OpenAIClientOptions options = new()
        {
            Diagnostics = { ApplicationId = HttpHeaderValues.UserAgent }
        };

        if (httpClient is not null)
        {
            options.Transport = new HttpClientTransport(httpClient);
            options.RetryPolicy = new RetryPolicy(maxRetries: 0); // Disable Azure SDK retry policy if and only if a custom HttpClient is provided.
        }

        return options;
    }

    /// <summary>
    /// Create a new empty chat instance
    /// </summary>
    /// <param name="text">Optional chat instructions for the AI service</param>
    /// <param name="executionSettings">Execution settings</param>
    /// <returns>Chat object</returns>
    internal static OpenAIChatHistory CreateNewChat(string? text = null, OpenAIPromptExecutionSettings? executionSettings = null)
    {
        // If settings is not provided, create a new chat with the text as the system prompt
        var chat = new OpenAIChatHistory(executionSettings?.ChatSystemPrompt ?? text);
        if (executionSettings is not null)
        {
            // If settings is provided, add the prompt as the user message
            chat.AddUserMessage(text!);
        }

        return chat;
    }

    private static CompletionsOptions CreateCompletionsOptions(string text, OpenAIPromptExecutionSettings executionSettings, string deploymentOrModelName)
    {
        if (executionSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(executionSettings)}.{nameof(executionSettings.ResultsPerPrompt)}", executionSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new CompletionsOptions
        {
            Prompts = { text.Replace("\r\n", "\n") }, // normalize line endings
            MaxTokens = executionSettings.MaxTokens,
            Temperature = (float?)executionSettings.Temperature,
            NucleusSamplingFactor = (float?)executionSettings.TopP,
            FrequencyPenalty = (float?)executionSettings.FrequencyPenalty,
            PresencePenalty = (float?)executionSettings.PresencePenalty,
            Echo = false,
            ChoicesPerPrompt = executionSettings.ResultsPerPrompt,
            GenerationSampleCount = executionSettings.ResultsPerPrompt,
            LogProbabilityCount = null,
            User = null,
            DeploymentName = deploymentOrModelName
        };

        foreach (var keyValue in executionSettings.TokenSelectionBiases)
        {
            options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
        }

        if (executionSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in executionSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        return options;
    }

    private static List<FunctionDefinition> NoFunctionToCall()
    {
        return new List<FunctionDefinition>(new[] { new FunctionDefinition("DontCallMe") { Parameters = BinaryData.FromString(@"{ ""type"": ""object"", ""properties"": {}, ""required"": [] }") } });
    }

    private static ChatCompletionsOptions CreateChatCompletionsOptions(
        OpenAIPromptExecutionSettings executionSettings,
        ChatHistory chatHistory,
        Kernel? kernel,
        string deploymentOrModelName)
    {
        if (executionSettings.ResultsPerPrompt is < 1 or > MaxResultsPerPrompt)
        {
            throw new ArgumentOutOfRangeException($"{nameof(executionSettings)}.{nameof(executionSettings.ResultsPerPrompt)}", executionSettings.ResultsPerPrompt, $"The value must be in range between 1 and {MaxResultsPerPrompt}, inclusive.");
        }

        var options = new ChatCompletionsOptions
        {
            MaxTokens = executionSettings.MaxTokens,
            Temperature = (float?)executionSettings.Temperature,
            NucleusSamplingFactor = (float?)executionSettings.TopP,
            FrequencyPenalty = (float?)executionSettings.FrequencyPenalty,
            PresencePenalty = (float?)executionSettings.PresencePenalty,
            ChoiceCount = executionSettings.ResultsPerPrompt,
            DeploymentName = deploymentOrModelName,
        };

        switch (executionSettings.FunctionCallBehavior)
        {
            case FunctionCallBehavior.KernelFunctions kfcb when kernel is not null:
                // Provide all of the functions available in the kernel.
                options.Functions = kernel.Plugins.GetFunctionsMetadata().Select(f => f.ToOpenAIFunction().ToFunctionDefinition()).ToList();
                if (options.Functions.Count > 0)
                {
                    options.FunctionCall = FunctionDefinition.Auto;
                }
                break;

            case FunctionCallBehavior.EnabledFunctions efcb:
                // Provide only those functions explicitly provided via the options.
                options.Functions = efcb.Functions;
                if (options.Functions.Count > 0)
                {
                    options.FunctionCall = FunctionDefinition.Auto;
                }
                break;

            case FunctionCallBehavior.RequiredFunction rufb:
                // Require the specific function provided via the options.
                options.Functions = rufb.FunctionArray;
                options.FunctionCall = rufb.Function;
                break;
        }

        foreach (var keyValue in executionSettings.TokenSelectionBiases)
        {
            options.TokenSelectionBiases.Add(keyValue.Key, keyValue.Value);
        }

        if (executionSettings.StopSequences is { Count: > 0 })
        {
            foreach (var s in executionSettings.StopSequences)
            {
                options.StopSequences.Add(s);
            }
        }

        foreach (var message in chatHistory)
        {
            options.Messages.Add(GetRequestMessage(message));
        }

        return options;
    }

    private static ChatRequestMessage GetRequestMessage(ChatRole chatRole, string contents, FunctionCall functionCall)
    {
        if (chatRole == ChatRole.User)
        {
            return new ChatRequestUserMessage(contents);
        }

        if (chatRole == ChatRole.System)
        {
            return new ChatRequestSystemMessage(contents);
        }

        if (chatRole == ChatRole.Assistant)
        {
            return new ChatRequestAssistantMessage(contents)
            {
                FunctionCall = functionCall
            };
        }

        if (chatRole == ChatRole.Function)
        {
            return new ChatRequestFunctionMessage(functionCall.Name, contents);
        }

        throw new NotImplementedException($"Role {chatRole} is not implemented");
    }

    private static ChatMessageContentItem GetChatMessageContentItem(ContentBase item)
    {
        return item switch
        {
            TextContent textContent => new ChatMessageTextContentItem(textContent.Text),
            ImageContent imageContent => new ChatMessageImageContentItem(imageContent.Uri),
            _ => throw new NotSupportedException($"Unsupported content type of chat message item: {item.GetType()}.")
        };
    }

    private static ChatRequestUserMessage GetChatRequestUserMessage(ChatMessageContent message, string? functionName)
    {
        if (message.Items is { Count: > 0 })
        {
            var contentItems = message.Items.Select(GetChatMessageContentItem);
            return new ChatRequestUserMessage(contentItems) { Name = functionName };
        }

        return new ChatRequestUserMessage(message.Content) { Name = functionName };
    }

    private static ChatRequestMessage GetRequestMessage(ChatMessageContent message)
    {
        ChatRequestMessage? requestMessage;
        var openAIMessage = message as OpenAIChatMessageContent;
        if (message.Role == AuthorRole.System)
        {
            requestMessage = new ChatRequestSystemMessage(message.Content);
        }
        else if (message.Role == AuthorRole.User)
        {
            string? functionName = null;
            if (message.Metadata?.TryGetValue(OpenAIChatMessageContent.FunctionNameProperty, out object? functionNameFromMetadata) is true)
            {
                functionName = functionNameFromMetadata?.ToString();
            }

            requestMessage = GetChatRequestUserMessage(message, functionName);
        }
        else if (message.Role == AuthorRole.Assistant)
        {
            requestMessage = new ChatRequestAssistantMessage(message.Content)
            {
                FunctionCall = openAIMessage?.FunctionCall,
            };
        }
        else if (string.Equals(message.Role.Label, "function", StringComparison.OrdinalIgnoreCase))
        {
            string? functionName = null;

            if (message.Metadata?.TryGetValue(OpenAIChatMessageContent.FunctionNameProperty, out object? functionNameFromMetadata) is true)
            {
                functionName = functionNameFromMetadata?.ToString();
            }
            else
            {
                throw new ArgumentException($"Function name was is not provided for {message.Role} role");
            }

            requestMessage = new ChatRequestFunctionMessage(functionName, message.Content);
        }
        else
        {
            // Tool and Custom Roles are not implemented yet
            throw new NotImplementedException($"Role {message.Role} is not implemented");
        }

        if (openAIMessage is null
            && message.Metadata?.TryGetValue(OpenAIChatMessageContent.FunctionNameProperty, out object? name) is true
            && requestMessage is ChatRequestAssistantMessage assistantMessage)
        {
            if (message.Metadata?.TryGetValue(OpenAIChatMessageContent.FunctionArgumentsProperty, out object? arguments) is true)
            {
                assistantMessage.FunctionCall = new FunctionCall(name?.ToString(), arguments?.ToString());
            }
            else
            {
                assistantMessage.Name = name?.ToString();
            }
        }

        return requestMessage;
    }

    private static ChatRequestMessage GetRequestMessage(ChatResponseMessage message)
    {
        if (message.Role == ChatRole.System)
        {
            return new ChatRequestSystemMessage(message.Content);
        }

        if (message.Role == ChatRole.Assistant)
        {
            return new ChatRequestAssistantMessage(message.Content);
        }

        if (message.Role == ChatRole.User)
        {
            return new ChatRequestUserMessage(message.Content);
        }

        if (message.Role == ChatRole.Function)
        {
            return new ChatRequestFunctionMessage(message.FunctionCall.Name, message.Content);
        }

        // TODO: Functin/Tool Calling
        throw new NotImplementedException($"Role {message.Role} is not implemented");
    }

    private static void ValidateMaxTokens(int? maxTokens)
    {
        if (maxTokens.HasValue && maxTokens < 1)
        {
            throw new ArgumentException($"MaxTokens {maxTokens} is not valid, the value must be greater than zero");
        }
    }

    private static void ValidateAutoInvoke(bool autoInvoke, int resultsPerPrompt)
    {
        if (autoInvoke && resultsPerPrompt != 1)
        {
            // We can remove this restriction in the future if valuable. However, multiple results per prompt is rare,
            // and limiting this significantly curtails the complexity of the implementation.
            throw new ArgumentException($"{nameof(FunctionCallBehavior)}.{nameof(FunctionCallBehavior.AutoInvoke)} may only be used with a {nameof(OpenAIPromptExecutionSettings.ResultsPerPrompt)} of 1.");
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
            throw e.ToHttpOperationException();
        }
    }

    /// <summary>
    /// Captures usage details, including token information.
    /// </summary>
    /// <param name="usage">Instance of <see cref="CompletionsUsage"/> with usage details.</param>
    private void CaptureUsageDetails(CompletionsUsage usage)
    {
        if (this.Logger.IsEnabled(LogLevel.Information))
        {
            this.Logger.LogInformation(
                "Prompt tokens: {PromptTokens}. Completion tokens: {CompletionTokens}. Total tokens: {TotalTokens}.",
                usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
        }

        s_promptTokensCounter.Add(usage.PromptTokens);
        s_completionTokensCounter.Add(usage.CompletionTokens);
        s_totalTokensCounter.Add(usage.TotalTokens);
    }
}
