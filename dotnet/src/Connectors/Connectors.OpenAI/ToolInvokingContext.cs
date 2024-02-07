﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Class with data related to tool before invocation.
/// </summary>
public sealed class ToolInvokingContext : ToolFilterContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolInvokingContext"/> class.
    /// </summary>
    /// <param name="toolCall">The <see cref="OpenAIFunctionToolCall"/> with which this filter is associated.</param>
    /// <param name="chatHistory">The chat history associated with the operation.</param>
    /// <param name="modelIterations">The number of model iterations completed thus far for the request.</param>
    public ToolInvokingContext(OpenAIFunctionToolCall toolCall, ChatHistory chatHistory, int modelIterations)
    : base(toolCall, chatHistory, modelIterations)
    {
    }
}
