﻿// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Connectors.AssemblyAI;

/// <summary>
/// Options to configure the AssemblyAIAudioToTextService
/// </summary>
public class AssemblyAIAudioToTextServiceOptions
{
    /// <summary>
    /// AssemblyAI API key, <a href="https://www.assemblyai.com/dashboard">get your API key from the dashboard.</a>
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The endpoint URL to the AssemblyAI API. Leave empty to use default endpoint.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// The time between each poll for the transcript status, until the status is completed.
    /// </summary>
    public TimeSpan? PollingInterval { get; set; }
}
