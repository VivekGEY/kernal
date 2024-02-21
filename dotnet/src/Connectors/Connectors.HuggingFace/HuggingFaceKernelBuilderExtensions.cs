﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.HuggingFace;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.TextGeneration;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides extension methods for the <see cref="IKernelBuilder"/> class to configure Hugging Face connectors.
/// </summary>
public static class HuggingFaceKernelBuilderExtensions
{
    /// <summary>
    /// Adds an Hugging Face text generation service with the specified configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IKernelBuilder"/> instance to augment.</param>
    /// <param name="model">The name of the Hugging Face model.</param>
    /// <param name="apiKey">The API key required for accessing the Hugging Face service.</param>
    /// <param name="endPoint">The endpoint URL for the text generation service.</param>
    /// <param name="serviceId">A local identifier for the given AI service.</param>
    /// <param name="httpClient">The HttpClient to use with this service.</param>
    /// <returns>The same instance as <paramref name="builder"/>.</returns>
    public static IKernelBuilder AddHuggingFaceTextGeneration(
        this IKernelBuilder builder,
        string model,
        string? apiKey = null,
        Uri? endPoint = null,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNull(model);

        builder.Services.AddKeyedSingleton<ITextGenerationService>(serviceId, (serviceProvider, _) =>
            new HuggingFaceTextGenerationService(model, endPoint, apiKey, HttpClientProvider.GetHttpClient(httpClient, serviceProvider)));

        return builder;
    }

    /// <summary>
    /// Adds an Hugging Face text generation service with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to augment.</param>
    /// <param name="model">The name of the Hugging Face model.</param>
    /// <param name="apiKey">The API key required for accessing the Hugging Face service.</param>
    /// <param name="endPoint">The endpoint URL for the text generation service.</param>
    /// <param name="serviceId">A local identifier for the given AI service.</param>
    /// <returns>The same instance as <paramref name="services"/>.</returns>
    public static IServiceCollection AddHuggingFaceTextGeneration(
        this IServiceCollection services,
        string model,
        string? apiKey = null,
        Uri? endPoint = null,
        string? serviceId = null)
    {
        Verify.NotNull(services);
        Verify.NotNull(model);

        return services.AddKeyedSingleton<ITextGenerationService>(serviceId, (serviceProvider, _) =>
            new HuggingFaceTextGenerationService(model, endPoint, apiKey, HttpClientProvider.GetHttpClient(serviceProvider)));
    }

    /// <summary>
    /// Adds an Hugging Face text embedding generation service with the specified configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IKernelBuilder"/> instance to augment.</param>
    /// <param name="model">The name of the Hugging Face model.</param>
    /// <param name="apiKey">The API key required for accessing the Hugging Face service.</param>
    /// <param name="endPoint">The endpoint for the text embedding generation service.</param>
    /// <param name="serviceId">A local identifier for the given AI service.</param>
    /// <param name="httpClient">The HttpClient to use with this service.</param>
    /// <returns>The same instance as <paramref name="builder"/>.</returns>
    public static IKernelBuilder AddHuggingFaceTextEmbeddingGeneration(
        this IKernelBuilder builder,
        string model,
        string? apiKey = null,
        Uri? endPoint = null,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        Verify.NotNull(builder);
        Verify.NotNull(model);

        builder.Services.AddKeyedSingleton<ITextEmbeddingGenerationService>(serviceId, (serviceProvider, _) =>
            new HuggingFaceTextEmbeddingGenerationService(model, endPoint, apiKey, HttpClientProvider.GetHttpClient(httpClient, serviceProvider)));

        return builder;
    }

    /// <summary>
    /// Adds an Hugging Face text embedding generation service with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to augment.</param>
    /// <param name="model">The name of the Hugging Face model.</param>
    /// <param name="apiKey">The API key required for accessing the Hugging Face service.</param>
    /// <param name="endPoint">The endpoint for the text embedding generation service.</param>
    /// <param name="serviceId">A local identifier for the given AI service.</param>
    /// <returns>The same instance as <paramref name="services"/>.</returns>
    public static IServiceCollection AddHuggingFaceTextEmbeddingGeneration(
        this IServiceCollection services,
        string model,
        string? apiKey = null,
        Uri? endPoint = null,
        string? serviceId = null)
    {
        Verify.NotNull(services);
        Verify.NotNull(model);

        return services.AddKeyedSingleton<ITextEmbeddingGenerationService>(serviceId, (serviceProvider, _) =>
            new HuggingFaceTextEmbeddingGenerationService(model, endPoint, apiKey, HttpClientProvider.GetHttpClient(serviceProvider)));
    }
}
