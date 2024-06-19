﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Http;
using OpenAI;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable IDE0039 // Use local function

/// <summary>
/// Sponsor class for OpenAI text embedding kernel builder extensions.
/// </summary>
public static class OpenAIKernelBuilderExtensions
{
    #region Text Embedding
    /// <summary>
    /// Adds the OpenAI text embeddings service to the list.
    /// </summary>
    /// <param name="builder">The <see cref="IServiceCollection"/> instance to augment.</param>
    /// <param name="options">Options for the OpenAI text embeddings service.</param>
    /// <param name="openAIClient"><see cref="OpenAIClient"/> to use for the service. If null, one must be available in the service provider when this service is resolved.</param>
    /// <returns>The same instance as <paramref name="builder"/>.</returns>
    [Experimental("SKEXP0010")]
    public static IKernelBuilder AddOpenAITextEmbeddingGeneration(
        this IKernelBuilder builder,
        OpenAITextEmbeddingGenerationConfig options,
        OpenAIClient? openAIClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNullOrWhiteSpace(options.ModelId);

        builder.Services.AddKeyedSingleton<ITextEmbeddingGenerationService>(options.ServiceId, (serviceProvider, _) =>
            new OpenAITextEmbeddingGenerationService(
                options,
                openAIClient ?? serviceProvider.GetRequiredService<OpenAIClient>()));

        return builder;
    }

    /// <summary>
    /// Adds the OpenAI text embeddings service to the list.
    /// </summary>
    /// <param name="builder">The <see cref="IKernelBuilder"/> instance to augment.</param>
    /// <param name="options">Options for the OpenAI text embeddings service.</param>
    /// <param name="httpClient">The HttpClient to use with this service.</param>
    /// <returns>The same instance as <paramref name="builder"/>.</returns>
    [Experimental("SKEXP0010")]
    public static IKernelBuilder AddOpenAITextEmbeddingGeneration(
        this IKernelBuilder builder,
        OpenAIClientTextEmbeddingGenerationConfig options,
        HttpClient? httpClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNullOrWhiteSpace(options.ModelId);
        Verify.NotNullOrWhiteSpace(options.ApiKey);

        builder.Services.AddKeyedSingleton<ITextEmbeddingGenerationService>(options.ServiceId, (serviceProvider, _) =>
            new OpenAITextEmbeddingGenerationService(
                options,
                HttpClientProvider.GetHttpClient(httpClient, serviceProvider)));

        return builder;
    }
    #endregion

    #region Chat Completion

    /// <summary>
    /// Adds the OpenAI chat completion service to the list.
    /// </summary>
    /// <param name="builder">The <see cref="IKernelBuilder"/> instance to augment.</param>
    /// <param name="config">OpenAI chat completion configuration</param>
    /// <param name="httpClient">The HttpClient to use with this service.</param>
    /// <returns>The same instance as <paramref name="builder"/>.</returns>
    public static IKernelBuilder AddOpenAIChatCompletion(
        this IKernelBuilder builder,
        OpenAIClientChatCompletionConfig config,
        HttpClient? httpClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNullOrWhiteSpace(config.ModelId);
        Verify.NotNullOrWhiteSpace(config.ApiKey);
        Func<IServiceProvider, object?, OpenAIChatCompletionService> factory = (serviceProvider, _) =>
        {
            config.LoggerFactory ??= serviceProvider.GetService<ILoggerFactory>();
            return new(config, HttpClientProvider.GetHttpClient(httpClient, serviceProvider));
        };

        builder.Services.AddKeyedSingleton<IChatCompletionService>(config.ServiceId, factory);

        return builder;
    }

    /// <summary>
    /// Adds the OpenAI chat completion service to the list.
    /// </summary>
    /// <param name="builder">The <see cref="IKernelBuilder"/> instance to augment.</param>
    /// <param name="config">OpenAI chat completion configuration</param>
    /// <param name="openAIClient"><see cref="OpenAIClient"/> to use for the service. If null, one must be available in the service provider when this service is resolved.</param>
    /// <returns>The same instance as <paramref name="builder"/>.</returns>
    public static IKernelBuilder AddOpenAIChatCompletion(
        this IKernelBuilder builder,
        OpenAIChatCompletionConfig config,
        OpenAIClient? openAIClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNullOrWhiteSpace(config.ModelId);

        Func<IServiceProvider, object?, OpenAIChatCompletionService> factory = (serviceProvider, _) =>
            new(config, openAIClient ?? serviceProvider.GetRequiredService<OpenAIClient>());

        builder.Services.AddKeyedSingleton<IChatCompletionService>(config.ServiceId, factory);

        return builder;
    }

    /// <summary>
    /// Adds the Custom Endpoint OpenAI chat completion service to the list.
    /// </summary>
    /// <param name="builder">The <see cref="IKernelBuilder"/> instance to augment.</param>
    /// <param name="modelId">OpenAI model name, see https://platform.openai.com/docs/models</param>
    /// <param name="endpoint">Custom OpenAI Compatible Message API endpoint</param>
    /// <param name="apiKey">OpenAI API key, see https://platform.openai.com/account/api-keys</param>
    /// <param name="orgId">OpenAI organization id. This is usually optional unless your account belongs to multiple organizations.</param>
    /// <param name="serviceId">A local identifier for the given AI service</param>
    /// <param name="httpClient">The HttpClient to use with this service.</param>
    /// <returns>The same instance as <paramref name="builder"/>.</returns>
    [Experimental("SKEXP0010")]
    //[Obsolete("Use the configuration paramether overload of this method instead.")]
    public static IKernelBuilder AddOpenAIChatCompletion(
        this IKernelBuilder builder,
        string modelId,
        Uri endpoint,
        string? apiKey,
        string? orgId = null,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNullOrWhiteSpace(modelId);

        Func<IServiceProvider, object?, OpenAIChatCompletionService> factory = (serviceProvider, _) =>
            new(new()
            {
                ModelId = modelId,
                ServiceId = serviceId,
                Endpoint = endpoint,
                ApiKey = apiKey,
                OrganizationId = orgId,
                LoggerFactory = serviceProvider.GetService<ILoggerFactory>()
            },
            httpClient: HttpClientProvider.GetHttpClient(httpClient, serviceProvider));

        builder.Services.AddKeyedSingleton<IChatCompletionService>(serviceId, factory);

        return builder;
    }
    #endregion
}
