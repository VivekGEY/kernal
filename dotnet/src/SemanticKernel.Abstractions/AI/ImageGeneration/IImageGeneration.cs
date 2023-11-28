﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel.AI.ImageGeneration;

/// <summary>
/// Interface for image generation services
/// </summary>
[Experimental("SKEXP0002")]
public interface IImageGeneration : IAIService
{
    /// <summary>
    /// Generate an image matching the given description
    /// </summary>
    /// <param name="description">Image description</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Generated image in base64 format or image URL</returns>
    [Experimental("SKEXP0002")]
    public Task<string> GenerateImageAsync(
        string description,
        int width,
        int height,
        CancellationToken cancellationToken = default);
}
