﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.SemanticKernel.Connectors.HuggingFace.Client.Models;

namespace Microsoft.SemanticKernel.Connectors.HuggingFace;

/// <summary>
/// Represents the metadata of a Hugging Face chat completion.
/// </summary>
public sealed class TextGenerationMetadata : ReadOnlyDictionary<string, object?>
{
    internal TextGenerationMetadata() : base(new Dictionary<string, object?>()) { }

    internal TextGenerationMetadata(TextGenerationResponse response) : this()
    {
        this.GeneratedTokens = response.FirstOrDefault()?.Details?.GeneratedTokens;
        this.FinishReason = response.FirstOrDefault()?.Details?.FinishReason;
        this.Tokens = response.FirstOrDefault()?.Details?.Tokens;
        this.PrefillTokens = response.FirstOrDefault()?.Details?.Prefill;
    }

    private TextGenerationMetadata(IDictionary<string, object?> dictionary) : base(dictionary) { }

    /// <summary>
    /// The list of tokens used on the generation.
    /// </summary>
    public object? Tokens
    {
        get => this.GetValueFromDictionary(nameof(this.Tokens));
        internal init => this.SetValueInDictionary(value, nameof(this.Tokens));
    }

    /// <summary>
    /// The list of prefill tokens used on the generation.
    /// </summary>
    public object? PrefillTokens
    {
        get => this.GetValueFromDictionary(nameof(this.PrefillTokens));
        internal init => this.SetValueInDictionary(value, nameof(this.PrefillTokens));
    }

    /// <summary>
    /// Number of generated tokens.
    /// </summary>
    public int? GeneratedTokens
    {
        get => this.GetValueFromDictionary(nameof(this.GeneratedTokens)) as int?;
        internal init => this.SetValueInDictionary(value, nameof(this.GeneratedTokens));
    }

    /// <summary>
    /// Finish reason.
    /// </summary>
    public string? FinishReason
    {
        get => this.GetValueFromDictionary(nameof(this.FinishReason)) as string;
        internal init => this.SetValueInDictionary(value, nameof(this.FinishReason));
    }

    /// <summary>
    /// Converts a dictionary to a <see cref="ChatCompletionMetadata"/> object.
    /// </summary>
    public static TextGenerationMetadata FromDictionary(IReadOnlyDictionary<string, object?> dictionary) => dictionary switch
    {
        null => throw new ArgumentNullException(nameof(dictionary)),
        TextGenerationMetadata metadata => metadata,
        IDictionary<string, object?> metadata => new TextGenerationMetadata(metadata),
        _ => new TextGenerationMetadata(dictionary.ToDictionary(pair => pair.Key, pair => pair.Value))
    };

    private void SetValueInDictionary(object? value, string propertyName)
        => this.Dictionary[propertyName] = value;

    private object? GetValueFromDictionary(string propertyName)
        => this.Dictionary.TryGetValue(propertyName, out var value) ? value : null;
}
