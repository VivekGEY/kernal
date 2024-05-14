﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.TemplateEngine;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Given a prompt, that might contain references to variables and functions:
/// - Get the list of references
/// - Resolve each reference
///   - Variable references are resolved using the context variables
///   - Function references are resolved invoking those functions
///     - Functions can be invoked passing in variables
///     - Functions do not receive the context variables, unless specified using a special variable
///     - Functions can be invoked in order and in parallel so the context variables must be immutable when invoked within the template
/// </summary>
internal sealed class KernelPromptTemplate : IPromptTemplate
{
    /// <summary>
    /// Constructor for PromptTemplate.
    /// </summary>
    /// <param name="promptConfig">Prompt template configuration</param>
    /// <param name="allowUnsafeContent">Flag indicating whether to allow unsafe content</param>
    /// <param name="loggerFactory">Logger factory</param>
    internal KernelPromptTemplate(PromptTemplateConfig promptConfig, bool allowUnsafeContent, ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(promptConfig, nameof(promptConfig));
        Verify.NotNull(promptConfig.Template, nameof(promptConfig.Template));

        loggerFactory ??= NullLoggerFactory.Instance;
        this._logger = loggerFactory.CreateLogger(typeof(KernelPromptTemplate)) ?? NullLogger.Instance;

        this._blocks = this.ExtractBlocks(promptConfig, loggerFactory);
        AddMissingInputVariables(this._blocks, promptConfig);

        this._allowUnsafeContent = allowUnsafeContent || promptConfig.AllowDangerouslySetContent;
        this._safeBlocks = new HashSet<string>(promptConfig.InputVariables.Where(iv => allowUnsafeContent || iv.AllowDangerouslySetContent).Select(iv => iv.Name));
    }

    /// <inheritdoc/>
    public Task<string> RenderAsync(Kernel kernel, KernelArguments? arguments = null, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(kernel);

        return this.RenderAsync(this._blocks, kernel, arguments, cancellationToken);
    }

    #region private
    private readonly ILogger _logger;
    private readonly List<Block> _blocks;
    private readonly bool _allowUnsafeContent;
    private readonly HashSet<string> _safeBlocks;

    /// <summary>
    /// Given a prompt template string, extract all the blocks (text, variables, function calls)
    /// </summary>
    /// <returns>A list of all the blocks, ie the template tokenized in text, variables and function calls</returns>
    private List<Block> ExtractBlocks(PromptTemplateConfig config, ILoggerFactory loggerFactory)
    {
        string templateText = config.Template;

        if (this._logger.IsEnabled(LogLevel.Trace))
        {
            this._logger.LogTrace("Extracting blocks from template: {0}", templateText);
        }

        var blocks = new TemplateTokenizer(loggerFactory).Tokenize(templateText);

        foreach (var block in blocks)
        {
            if (!block.IsValid(out var error))
            {
                throw new KernelException(error);
            }
        }

        return blocks;
    }

    /// <summary>
    /// Given a list of blocks render each block and compose the final result.
    /// </summary>
    /// <param name="blocks">Template blocks generated by ExtractBlocks.</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The prompt template ready to be used for an AI request.</returns>
    private async Task<string> RenderAsync(List<Block> blocks, Kernel kernel, KernelArguments? arguments, CancellationToken cancellationToken = default)
    {
        var result = new StringBuilder();
        foreach (var block in blocks)
        {
            string? blockResult = null;
            switch (block)
            {
                case ITextRendering staticBlock:
                    blockResult = InternalTypeConverter.ConvertToString(staticBlock.Render(arguments), kernel.Culture);
                    break;

                case ICodeRendering dynamicBlock:
                    blockResult = InternalTypeConverter.ConvertToString(await dynamicBlock.RenderCodeAsync(kernel, arguments, cancellationToken).ConfigureAwait(false), kernel.Culture);
                    break;

                default:
                    Debug.Fail($"Unexpected block type {block?.GetType()}, the block doesn't have a rendering method");
                    break;
            }

            if (blockResult is not null)
            {
                if (ShouldEncodeTags(this._allowUnsafeContent, this._safeBlocks, block!))
                {
                    blockResult = HttpUtility.HtmlEncode(blockResult);
                }
                result.Append(blockResult);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Augments <paramref name="config"/>'s <see cref="PromptTemplateConfig.InputVariables"/> with any variables
    /// not already contained there but that are referenced in the prompt template.
    /// </summary>
    private static void AddMissingInputVariables(List<Block> blocks, PromptTemplateConfig config)
    {
        // Add all of the existing input variables to our known set. We'll avoid adding any
        // dynamically discovered input variables with the same name.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InputVariable iv in config.InputVariables)
        {
            seen.Add(iv.Name);
        }

        // Enumerate every block in the template, adding any variables that are referenced.
        foreach (Block block in blocks)
        {
            switch (block.Type)
            {
                case BlockTypes.Variable:
                    // Add all variables from variable blocks, e.g. "{{$a}}".
                    AddIfMissing(((VarBlock)block).Name);
                    break;

                case BlockTypes.Code:
                    foreach (Block codeBlock in ((CodeBlock)block).Blocks)
                    {
                        switch (codeBlock.Type)
                        {
                            case BlockTypes.Variable:
                                // Add all variables from code blocks, e.g. "{{p.bar $b}}".
                                AddIfMissing(((VarBlock)codeBlock).Name);
                                break;

                            case BlockTypes.NamedArg when ((NamedArgBlock)codeBlock).VarBlock is { } varBlock:
                                // Add all variables from named arguments, e.g. "{{p.bar b = $b}}".
                                AddIfMissing(varBlock.Name);
                                break;
                        }
                    }
                    break;
            }
        }

        void AddIfMissing(string variableName)
        {
            if (!string.IsNullOrEmpty(variableName) && seen.Add(variableName))
            {
                config.InputVariables.Add(new InputVariable { Name = variableName });
            }
        }
    }

    private static bool ShouldEncodeTags(bool disableTagEncoding, HashSet<string> safeBlocks, Block block)
    {
        if (block is VarBlock varBlock)
        {
            return !safeBlocks.Contains(varBlock.Name);
        }

        return !disableTagEncoding && block is not TextBlock;
    }

    #endregion
}
