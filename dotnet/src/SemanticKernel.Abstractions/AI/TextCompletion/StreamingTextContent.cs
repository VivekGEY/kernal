﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text;

namespace Microsoft.SemanticKernel.AI.TextCompletion;

/// <summary>
/// Streaming text result update.
/// </summary>
public class StreamingTextContent : StreamingContent
{
    /// <summary>
    /// Text associated to the update
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// The encoding of the text content.
    /// </summary>
    public Encoding Encoding { get; set; }

    /// <summary>
    /// Create a new instance of the <see cref="StreamingTextContent"/> class.
    /// </summary>
    /// <param name="text">Text update</param>
    /// <param name="choiceIndex">Index of the choice</param>
    /// <param name="innerContent">Inner chunk object</param>
    /// <param name="encoding">Encoding of the text</param>
    /// <param name="metadata">Metadata information</param>
    public StreamingTextContent(string? text, int choiceIndex = 0, object? innerContent = null, Encoding? encoding = null, IReadOnlyDictionary<string, object?>? metadata = null) : base(innerContent, choiceIndex, metadata)
    {
        this.Text = text;
        this.Encoding = encoding ?? Encoding.UTF8;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Text ?? string.Empty;
    }

    /// <inheritdoc/>
    public override byte[] ToByteArray()
    {
        return this.Encoding.GetBytes(this.ToString());
    }
}
