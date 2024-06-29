﻿// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.SemanticKernel.TemplateEngine;

/// <summary>
/// Interface of static blocks that do not need async IO to be rendered.
/// </summary>
internal interface ITextRendering
{
    /// <summary>
    /// Render the block using only the given arguments.
    /// </summary>
    /// <param name="arguments">Optional arguments the block rendering</param>
    /// <returns>Rendered content</returns>
    public object? Render(KernelArguments? arguments);
}
