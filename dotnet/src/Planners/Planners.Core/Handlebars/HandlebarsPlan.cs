﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using Microsoft.SemanticKernel.Orchestration;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.SemanticKernel.Planning.Handlebars;
#pragma warning restore IDE0130

/// <summary>
/// Represents a Handlebars plan.
/// </summary>
public sealed class HandlebarsPlan
{
    private readonly Kernel _kernel;
    private readonly string _template;

    /// <summary>
    /// Gets the prompt template used to generate the plan.
    /// </summary>
    /// <returns>The prompt template.</returns>
    public string Prompt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlebarsPlan"/> class.
    /// </summary>
    /// <param name="kernel">Kernel instance.</param>
    /// <param name="template">A Handlebars template representing the plan.</param>
    /// <param name="createPlanPromptTemplate">Prompt template used to generate the plan.</param>
    public HandlebarsPlan(Kernel kernel, string template, string createPlanPromptTemplate)
    {
        this._kernel = kernel;
        this._template = template;
        this.Prompt = createPlanPromptTemplate;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this._template;
    }

    /// <summary>
    /// Invokes the Handlebars plan.
    /// </summary>
    /// <param name="executionContext">The execution context.</param>
    /// <param name="variables">The variables.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The plan result.</returns>
    public FunctionResult Invoke(
        SKContext executionContext,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        string? results = HandlebarsTemplateEngineExtensions.Render(this._kernel, executionContext, this._template, variables, cancellationToken);
        executionContext.Variables.Update(results);
        return new FunctionResult("HandlebarsPlanner", executionContext, results?.Trim());
    }
}
