﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using SemanticKernel.Service.Model;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class SemanticKernelController : ControllerBase, IDisposable
{
    private readonly ILogger<SemanticKernelController> _logger;
    private readonly List<IDisposable> _disposables;

    public SemanticKernelController(ILogger<SemanticKernelController> logger)
    {
        this._logger = logger;
        this._disposables = new List<IDisposable>();
    }

    /// <summary>
    /// Invoke a Semantic Kernel function on the server.
    /// </summary>
    /// <remarks>
    /// We create and use a new kernel for each request.
    /// We feed the kernel the ask received via POST from the client
    /// and attempt to invoke the function with the given name.
    /// </remarks>
    /// <param name="kernel">Semantic kernel obtained through dependency injection</param>
    /// <param name="ask">Prompt along with its parameters</param>
    /// <param name="skillName">Skill in which function to invoke resides</param>
    /// <param name="functionName">Name of function to invoke</param>
    /// <returns>Results consisting of text generated by invoked function along with the variable in the SK that generated it</returns>
    [Authorize]
    [Route("skills/{skillName}/functions/{functionName}/invoke")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AskResult>> InvokeFunctionAsync(
        [FromServices] IKernel kernel,
        [FromBody] Ask ask,
        string skillName, string functionName)
    {
        this._logger.LogDebug("Received call to invoke {SkillName}/{FunctionName}", skillName, functionName);

        if (string.IsNullOrWhiteSpace(ask.Input))
        {
            return this.BadRequest("Input is required.");
        }

        // Put ask's variables in the context we will use.
        var contextVariables = new ContextVariables(ask.Input);
        foreach (var input in ask.Variables)
        {
            contextVariables.Set(input.Key, input.Value);
        }

        // Get the function to invoke
        ISKFunction? function = null;
        try
        {
            function = kernel.Skills.GetFunction(skillName, functionName);
        }
        catch (KernelException)
        {
            return this.NotFound($"Failed to find {skillName}/{functionName} on server");
        }

        // Run the function.
        SKContext result = await kernel.RunAsync(contextVariables, function!);
        if (result.ErrorOccurred)
        {
            if (result.LastException is AIException aiException && aiException.Detail is not null)
            {
                return this.BadRequest(string.Concat(aiException.Message, " - Detail: " + aiException.Detail));
            }

            return this.BadRequest(result.LastErrorDescription);
        }

        return this.Ok(new AskResult { Value = result.Result, Variables = result.Variables.Select(v => new KeyValuePair<string, string>(v.Key, v.Value)) });
    }

    /// <summary>
    /// Dispose of the object.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (IDisposable disposable in this._disposables)
            {
                disposable.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
