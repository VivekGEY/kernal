﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.PromptTemplates.Handlebars;

/// <summary>
/// Provides an <see cref="IPromptTemplateFactory"/> for the handlebars prompt template format.
/// </summary>
public sealed class HandlebarsPromptTemplateFactory : IPromptTemplateFactory
{
    /// <summary>Gets the name of the Handlebars template format.</summary>
    public static string HandlebarsTemplateFormat => "handlebars";

    /// <summary>
    /// Default options for built-in Handlebars helpers.
    /// </summary>
    /// TODO [@teresaqhoang]: Support override of default options
    private readonly HandlebarsPromptTemplateOptions _options;

    /// <summary>
    /// The character used to delimit plugin, function, or variable names in a Handlebars template.
    /// </summary>
    public string NameDelimiter => this._options.PrefixSeparator;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlebarsPromptTemplateFactory"/> class.
    /// </summary>
    /// <param name="options">Handlebars promnpt template options</param>
    public HandlebarsPromptTemplateFactory(HandlebarsPromptTemplateOptions? options = null)
    {
        this._options = options ?? new();
    }

    /// <inheritdoc/>
    public bool TryCreate(PromptTemplateConfig templateConfig, [NotNullWhen(true)] out IPromptTemplate? result)
    {
        Verify.NotNull(templateConfig);

        if (templateConfig.TemplateFormat.Equals(HandlebarsTemplateFormat, System.StringComparison.Ordinal))
        {
            result = new HandlebarsPromptTemplate(templateConfig, this.AllowUnsafeContent, this._options);
            return true;
        }

        result = null;
        return false;
    }
}
