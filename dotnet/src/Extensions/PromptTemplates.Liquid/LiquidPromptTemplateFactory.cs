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

    /// <inheritdoc/>
    public bool TryCreate(PromptTemplateConfig templateConfig, [NotNullWhen(true)] out IPromptTemplate? result)
    {
        Verify.NotNull(templateConfig);

        if (LiquidTemplateFormat.Equals(templateConfig.TemplateFormat, StringComparison.Ordinal))
        {
            result = new LiquidPromptTemplate(templateConfig);
            return true;
        }

        result = null;
        return false;
    }
}
