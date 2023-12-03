﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.OpenAI;

/// <summary>
/// Streaming text result update.
/// </summary>
public class OpenAIStreamingTextContent : StreamingTextContent
{
    /// <summary>
    /// Create a new instance of the <see cref="OpenAIStreamingTextContent"/> class.
    /// </summary>
    /// <param name="text">Text update</param>
    /// <param name="choiceIndex">Index of the choice</param>
    /// <param name="innerContentObject">Inner chunk object</param>
    /// <param name="metadata">Metadata information</param>
    public OpenAIStreamingTextContent(string text, int choiceIndex, object? innerContentObject = null, Dictionary<string, object>? metadata = null)
        : base(text, choiceIndex, innerContentObject, metadata)
    {
    }

    /// <inheritdoc/>
    public override byte[] ToByteArray()
    {
        return Encoding.UTF8.GetBytes(this.ToString());
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Text ?? string.Empty;
    }
}
