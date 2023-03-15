﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.TemplateEngine.Blocks;

internal class ValBlock : Block, ITextRendering
{
    internal override BlockTypes Type => BlockTypes.Value;

    // Cache the first and last char
    private readonly char _first = '\0';
    private readonly char _last = '\0';

    // Content, excluding start/end quote chars
    private readonly string _value = string.Empty;

    /// <summary>
    /// Create an instance
    /// </summary>
    /// <param name="quotedValue">Block content, including the delimiting chars</param>
    /// <param name="log">Optional logger</param>
    public ValBlock(string? quotedValue, ILogger? log = null)
        : base(quotedValue?.Trim(), log)
    {
        if (this.Content.Length < 2)
        {
            this.Log.LogError("A value must have single quotes or double quotes on both sides");
            return;
        }

        this._first = this.Content[0];
        this._last = this.Content[^1];
        this._value = this.Content[1..^1];
    }

#pragma warning disable CA2254 // error strings are used also internally, not just for logging
    // ReSharper disable TemplateIsNotCompileTimeConstantProblem
    public override bool IsValid(out string errorMsg)
    {
        errorMsg = string.Empty;

        // Content includes the quotes, so it must be at least 2 chars long
        if (this.Content.Length < 2)
        {
            errorMsg = "A value must have single quotes or double quotes on both sides";
            this.Log.LogError(errorMsg);
            return false;
        }

        // Check if delimiting chars are consistent
        if (this._first != this._last)
        {
            errorMsg = "A value must be defined using either single quotes or double quotes, not both";
            this.Log.LogError(errorMsg);
            return false;
        }

        return true;
    }
#pragma warning restore CA2254

    public string Render(ContextVariables? variables)
    {
        return this._value;
    }

    public static bool HasValPrefix(string? text)
    {
        return !string.IsNullOrEmpty(text)
               && text.Length > 0
               && (text[0] is Symbols.DblQuote or Symbols.SglQuote);
    }
}
