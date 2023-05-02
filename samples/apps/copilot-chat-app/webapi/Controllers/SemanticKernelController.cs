﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using SemanticKernel.Service.Config;
using SemanticKernel.Service.Model;
using SemanticKernel.Service.Skills;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service.Controllers;

[ApiController]
public class SemanticKernelController : ControllerBase
{
    private readonly ILogger<SemanticKernelController> _logger;
    private readonly PromptSettings _promptSettings;
    private readonly ServiceOptions _options;

    public SemanticKernelController(
        IOptions<ServiceOptions> options,
        PromptSettings promptSettings,
        ILogger<SemanticKernelController> logger)
    {
        this._logger = logger;
        this._options = options.Value;
        this._promptSettings = promptSettings;
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
    /// <param name="chatRepository">Storage repository to store chat sessions</param>
    /// <param name="chatMessageRepository">Storage repository to store chat messages</param>
    /// <param name="documentMemoryOptions">Options for document memory handling.</param>
    /// <param name="planner">Planner to use to create function sequences.</param>
    /// <param name="plannerOptions">Options for the planner.</param>
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
        [FromServices] ChatSessionRepository chatRepository,
        [FromServices] ChatMessageRepository chatMessageRepository,
        [FromServices] IOptions<DocumentMemoryOptions> documentMemoryOptions,
        [FromServices] CopilotChatPlanner planner,
        [FromServices] IOptions<PlannerOptions> plannerOptions,
        [FromBody] Ask ask,
        string skillName, string functionName)
    {
        this._logger.LogDebug("Received call to invoke {SkillName}/{FunctionName}", skillName, functionName);

        if (string.IsNullOrWhiteSpace(ask.Input))
        {
            return this.BadRequest("Input is required.");
        }

        // Not required for Copilot Chat, but this is how to register additional skills for the service to provide.
        if (!string.IsNullOrWhiteSpace(this._options.SemanticSkillsDirectory))
        {
            kernel.RegisterSemanticSkills(this._options.SemanticSkillsDirectory, this._logger);
        }

        // Register skills with the planner if enabled.
        if (plannerOptions.Value.Enabled)
        {
            await this.RegisterPlannerSkillsAsync(planner, plannerOptions.Value);
        }

        // Register native skills with the chat's kernel
        kernel.RegisterNativeSkills(
            chatSessionRepository: chatRepository,
            chatMessageRepository: chatMessageRepository,
            promptSettings: this._promptSettings,
            planner: planner,
            plannerOptions: plannerOptions.Value,
            documentMemoryOptions: documentMemoryOptions.Value,
            logger: this._logger);

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

        // Put ask's variables in the context we will use.
        var contextVariables = new ContextVariables(ask.Input);
        foreach (var input in ask.Variables)
        {
            contextVariables.Set(input.Key, input.Value);
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
    /// Register skills with the planner's kernel.
    /// </summary>
    private async Task RegisterPlannerSkillsAsync(CopilotChatPlanner planner, PlannerOptions options)
    {
        //await planner.Kernel.ImportChatGptPluginSkillFromUrlAsync("KlarnaShopping", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"));
        await planner.Kernel.ImportOpenApiSkillFromFileAsync("KlarnaShopping", @"C:\Users\adribona\Desktop\api-docs.json");
    }
}
