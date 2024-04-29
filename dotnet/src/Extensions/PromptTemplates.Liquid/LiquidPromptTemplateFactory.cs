﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.PromptTemplates.Liquid;

/// <summary>
/// Provides an <see cref="IPromptTemplateFactory"/> for liquid template format.
/// </summary>
public sealed class LiquidPromptTemplateFactory : IPromptTemplateFactory
{
    /// <summary>
    /// Gets the name of the liquid template format.
    /// </summary>
    public static string LiquidTemplateFormat => "liquid";

    /// <summary>
    /// Gets or sets a value indicating whether to allow unsafe content.
    /// </summary>
    /// <remarks>
    /// The default is false.
    /// When set to true then all input content added to templates is treated as safe content and will not be HTML encoded.
    /// For prompts which are being used with a chat completion service this should be set to false to protect against prompt injection attacks.
    /// When using other AI services e.g. Text-To-Image this can be set to true to allow for more complex prompts.
    /// </remarks>
    [Experimental("SKEXP0001")]
    public bool AllowUnsafeContent { get; init; } = false;

    /// <inheritdoc/>
    public bool TryCreate(PromptTemplateConfig templateConfig, [NotNullWhen(true)] out IPromptTemplate? result)
    {
        if (templateConfig.TemplateFormat.Equals(LiquidTemplateFormat, StringComparison.Ordinal))
        {
            result = new LiquidPromptTemplate(templateConfig, this.AllowUnsafeContent);
            return true;
        }

        result = null;
        return false;
    }
}