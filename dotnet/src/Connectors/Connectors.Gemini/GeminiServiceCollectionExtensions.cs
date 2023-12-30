﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Gemini;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.TextGeneration;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Extensions for adding Gemini text generation services to the application.
/// </summary>
public static class GeminiServiceCollectionExtensions
{
    /// <summary>
    /// Adds Gemini Text Generation service to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="modelId">The model for text generation.</param>
    /// <param name="apiKey">The API key for authentication Gemini API.</param>
    /// <param name="serviceId">The optional service ID.</param>
    /// <param name="httpClient">The optional custom HttpClient.</param>
    /// <returns>The updated kernel builder.</returns>
    public static IKernelBuilder AddGeminiTextGeneration(
        this IKernelBuilder builder,
        string modelId,
        string apiKey,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNull(modelId);
        Verify.NotNull(apiKey);

        builder.Services.AddKeyedSingleton<ITextGenerationService>(serviceId, (serviceProvider, _) =>
            new GeminiTextGenerationService(modelId, apiKey, HttpClientProvider.GetHttpClient(httpClient, serviceProvider)));
        return builder;
    }

    /// <summary>
    /// Adds Gemini Text Generation service to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add the Gemini Text Generation service to.</param>
    /// <param name="modelId">The model for text generation.</param>
    /// <param name="apiKey">The API key for authentication Gemini API.</param>
    /// <param name="serviceId">Optional service ID.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGeminiTextGeneration(
        this IServiceCollection services,
        string modelId,
        string apiKey,
        string? serviceId = null)
    {
        Verify.NotNull(services);
        Verify.NotNull(modelId);
        Verify.NotNull(apiKey);

        return services.AddKeyedSingleton<ITextGenerationService>(serviceId, (serviceProvider, _) =>
            new GeminiTextGenerationService(modelId, apiKey, HttpClientProvider.GetHttpClient(serviceProvider)));
    }

    /// <summary>
    /// Adds Gemini Chat Completion service to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="modelId">The model for text generation.</param>
    /// <param name="apiKey">The API key for authentication Gemini API.</param>
    /// <param name="serviceId">The optional service ID.</param>
    /// <param name="httpClient">The optional custom HttpClient.</param>
    /// <returns>The updated kernel builder.</returns>
    public static IKernelBuilder AddGeminiChatCompletion(
        this IKernelBuilder builder,
        string modelId,
        string apiKey,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNull(modelId);
        Verify.NotNull(apiKey);

        builder.Services.AddKeyedSingleton<IChatCompletionService>(serviceId, (serviceProvider, _) =>
            new GeminiChatCompletionService(modelId, apiKey, HttpClientProvider.GetHttpClient(httpClient, serviceProvider)));
        return builder;
    }

    /// <summary>
    /// Adds Gemini Chat Completion service to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add the Gemini Text Generation service to.</param>
    /// <param name="modelId">The model for text generation.</param>
    /// <param name="apiKey">The API key for authentication Gemini API.</param>
    /// <param name="serviceId">Optional service ID.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddGeminiChatCompletion(
        this IServiceCollection services,
        string modelId,
        string apiKey,
        string? serviceId = null)
    {
        Verify.NotNull(services);
        Verify.NotNull(modelId);
        Verify.NotNull(apiKey);

        return services.AddKeyedSingleton<IChatCompletionService>(serviceId, (serviceProvider, _) =>
            new GeminiChatCompletionService(modelId, apiKey, HttpClientProvider.GetHttpClient(serviceProvider)));
    }
}
