﻿// Copyright (c) Microsoft. All rights reserved.

// ReSharper disable once CheckNamespace - Using the namespace of Kernel
using Microsoft.SemanticKernel.AI;

#pragma warning disable IDE0130
namespace Microsoft.SemanticKernel.Planning;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for PromptTemplateConfig
/// </summary>
internal static class PromptTemplateConfigExtensions
{
    /// <summary>
    /// Set the max_tokens request setting to be used by OpenAI models
    /// </summary>
    /// <param name="config">PromptTemplateConfig instance</param>
    /// <param name="maxTokens">Value of max tokens to set</param>
    internal static void SetMaxTokens(this PromptTemplateConfig config, int maxTokens)
    {
        PromptExecutionSettings requestSettings = config.GetDefaultRequestSettings() ?? new();
        if (config.ModelSettings.Count == 0)
        {
            config.ModelSettings.Add(requestSettings);
        }
        requestSettings.ExtensionData["max_tokens"] = maxTokens;
    }
}
