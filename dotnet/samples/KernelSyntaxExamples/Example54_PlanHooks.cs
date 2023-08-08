﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example54_PlanHooks
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== Sequential Planner - Using Step Hooks ========");
        var kernel = new KernelBuilder()
            .WithLogger(ConsoleLogger.Logger)
            .WithAzureTextCompletionService(
                TestConfiguration.AzureOpenAI.DeploymentName,
                TestConfiguration.AzureOpenAI.Endpoint,
                TestConfiguration.AzureOpenAI.ApiKey)
            .Build();

        string folder = RepoFiles.SampleSkillsPath();
        kernel.ImportSemanticSkillFromDirectory(folder,
            "SummarizeSkill",
            "WriterSkill");

        var planner = new SequentialPlanner(kernel);

        var plan = await planner.CreatePlanAsync("Write a poem about John Doe, then translate it into Italian.");

        Console.WriteLine("Original plan:");
        Console.WriteLine(plan.ToPlanString());

        plan.SetPreExecutionHook(MyPreHook);
        plan.SetPostExecutionHook(MyPostHook);

        var result = await kernel.RunAsync(plan);

        Console.WriteLine("Result:");
        Console.WriteLine(result.Result);

        Task MyPreHook(PreExecutionContext executionContext)
        {
            Console.WriteLine($"Pre Hook - Prompt: {executionContext.Prompt}");

            return Task.CompletedTask;
        }

        Task MyPostHook(PostExecutionContext executionContext)
        {
            Console.WriteLine($"Post Hook - Total Tokens: {executionContext.SKContext.ModelResults.First().GetOpenAITextResult().Usage.TotalTokens}");

            return Task.CompletedTask;
        }
    }
}
