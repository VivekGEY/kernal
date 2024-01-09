﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

public class Example56_TemplateMethodFunctionsWithMultipleArguments : BaseTest
{
    /// <summary>
    /// Show how to invoke a Method Function written in C# with multiple arguments
    /// from a Prompt Function written in natural language
    /// </summary>
    [Fact]
    public async Task RunAsync()
    {
        this._output.WriteLine("======== TemplateMethodFunctionsWithMultipleArguments ========");

        string serviceId = TestConfiguration.AzureOpenAI.ServiceId;
        string apiKey = TestConfiguration.AzureOpenAI.ApiKey;
        string deploymentName = TestConfiguration.AzureOpenAI.ChatDeploymentName;
        string modelId = TestConfiguration.AzureOpenAI.ChatModelId;
        string endpoint = TestConfiguration.AzureOpenAI.Endpoint;

        if (apiKey == null || deploymentName == null || modelId == null || endpoint == null)
        {
            this._output.WriteLine("AzureOpenAI modelId, endpoint, apiKey, or deploymentName not found. Skipping example.");
            return;
        }

        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.Services.AddLogging(c => c.AddConsole());
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: deploymentName,
            endpoint: endpoint,
            serviceId: serviceId,
            apiKey: apiKey,
            modelId: modelId);
        Kernel kernel = builder.Build();

        var arguments = new KernelArguments();
        arguments["word2"] = " Potter";

        // Load native plugin into the kernel function collection, sharing its functions with prompt templates
        // Functions loaded here are available as "text.*"
        kernel.ImportPluginFromType<TextPlugin>("text");

        // Prompt Function invoking text.Concat method function with named arguments input and input2 where input is a string and input2 is set to a variable from context called word2.
        const string FunctionDefinition = @"
 Write a haiku about the following: {{text.Concat input='Harry' input2=$word2}}
";

        // This allows to see the prompt before it's sent to OpenAI
        this._output.WriteLine("--- Rendered Prompt");
        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var promptTemplate = promptTemplateFactory.Create(new PromptTemplateConfig(FunctionDefinition));
        var renderedPrompt = await promptTemplate.RenderAsync(kernel, arguments);
        this._output.WriteLine(renderedPrompt);

        // Run the prompt / prompt function
        var haiku = kernel.CreateFunctionFromPrompt(FunctionDefinition, new OpenAIPromptExecutionSettings() { MaxTokens = 100 });

        // Show the result
        this._output.WriteLine("--- Prompt Function result");
        var result = await kernel.InvokeAsync(haiku, arguments);
        this._output.WriteLine(result.GetValue<string>());

        /* OUTPUT:

--- Rendered Prompt

 Write a haiku about the following: Harry Potter

--- Prompt Function result
A boy with a scar,
Wizarding world he explores,
Harry Potter's tale.
         */
    }

    public Example56_TemplateMethodFunctionsWithMultipleArguments(ITestOutputHelper output) : base(output)
    {
    }
}
