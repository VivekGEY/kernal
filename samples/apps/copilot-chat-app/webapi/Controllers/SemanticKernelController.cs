﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using SemanticKernel.Service.Auth;
using SemanticKernel.Service.CopilotChat.Storage;
using SemanticKernel.Service.Models;
using SemanticKernel.Service.Utilities;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class SemanticKernelController : ControllerBase
{
    private readonly ILogger<SemanticKernelController> _logger;

    public SemanticKernelController(ILogger<SemanticKernelController> logger)
    {
        this._logger = logger;
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
    /// <param name="askConverter">Converter to use for converting Asks.</param>
    /// <param name="chatSessionRepository">Storage for chat sessions.</param>
    /// <param name="authInfo">Authenticated info about the user for the current request.</param>
    /// <param name="ask">Prompt along with its parameters</param>
    /// <param name="skillName">Skill in which function to invoke resides</param>
    /// <param name="functionName">Name of function to invoke</param>
    /// <returns>Results consisting of text generated by invoked function along with the variable in the SK that generated it</returns>
    [Route("skills/{skillName}/functions/{functionName}/invoke")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AskResult>> InvokeFunctionAsync(
        [FromServices] IKernel kernel,
        [FromServices] AskConverter askConverter,
        [FromServices] ChatSessionRepository chatSessionRepository,
        [FromServices] IAuthInfo authInfo,
        [FromBody] Ask ask,
        string skillName, string functionName)
    {
        this._logger.LogDebug("Received call to invoke {SkillName}/{FunctionName}", skillName, functionName);

        const string chatIdKey = "chatId";
        var chatIdFromContext = ask.Variables.FirstOrDefault(x => x.Key == chatIdKey);
        if (chatIdFromContext.Key is chatIdKey)
        {
            var chat = await chatSessionRepository.FindByIdAsync(chatIdFromContext.Value);
            if (chat == null)
            {
                return this.NotFound("Failed to find chat session for the chatId specified in variables.");
            }
            if (chat.UserId != authInfo.UserId)
            {
                return this.Unauthorized("User does not have access to the chatId specified in variables.");
            }
        }

        // Put ask's variables in the context we will use.
        var contextVariables = askConverter.GetContextVariables(ask);

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
}
