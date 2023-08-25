﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.Tokenizers;
using Microsoft.SemanticKernel.Events;
using RepoUtils;

#pragma warning disable RCS1214 // Unnecessary interpolated string.

// ReSharper disable once InconsistentNaming
public static class Example56_FunctionEventHandlers
{
    private static string? openAIModelId;
    private static string? openAIApiKey;

    public static async Task RunAsync()
    {
        Console.WriteLine("\n======== Using Function Execution Handlers ========\n");

        openAIModelId = TestConfiguration.OpenAI.ModelId;
        openAIApiKey = TestConfiguration.OpenAI.ApiKey;

        if (openAIModelId == null || openAIApiKey == null)
        {
            Console.WriteLine("OpenAI credentials not found. Skipping example.");
            return;
        }

        await GetPromptAndUsageAsync();

        await ChangingResultAsync();

        await CancellingFunctionAsync();

        await SkippingFunctionAsync();
    }

    private static async Task GetPromptAndUsageAsync()
    {
        Console.WriteLine("\n======== Get Rendered Prompt and Usage Data ========\n");

        IKernel kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAITextCompletionService(
                modelId: openAIModelId!,
                apiKey: openAIApiKey!)
            .Build();

        const string functionPrompt = "Write a paragraph about: {{$input}}.";

        var excuseFunction = kernel.CreateSemanticFunction(
            functionPrompt,
            skillName: "MySkill",
            functionName: "Excuse",
            maxTokens: 100,
            temperature: 0.4,
            topP: 1);

        void MyPreHandler(object? sender, FunctionInvokingEventArgs e)
        {
            if (e is SemanticFunctionInvokingEventArgs se)
            {
                Console.WriteLine($"{se.FunctionView.SkillName}.{se.FunctionView.Name} : Pre Execution Handler - Rendered Prompt: {se.RenderedPrompt}");
            }
        }

        void MyRemovedPreExecutionHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.SkillName}.{e.FunctionView.Name} : Pre Execution Handler - Should not trigger");
            e.Cancel();
        }

        void MyPreHandler2(object? sender, FunctionInvokingEventArgs e)
        {
            if (e is SemanticFunctionInvokingEventArgs se)
            {
                Console.WriteLine($"{se.FunctionView.SkillName}.{se.FunctionView.Name} : Pre Execution Handler 2 - Rendered Prompt Token: {GPT3Tokenizer.Encode(se.RenderedPrompt!).Count}");
            }
        }

        void MyPostExecutionHandler(object? sender, FunctionInvokedEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.SkillName}.{e.FunctionView.Name} : Post Execution Handler - Total Tokens: {e.SKContext.ModelResults.First().GetOpenAITextResult().Usage.TotalTokens}");
        }

        kernel.FunctionInvoking += MyPreHandler;
        kernel.FunctionInvoking += MyPreHandler2;
        kernel.FunctionInvoking += MyRemovedPreExecutionHandler;
        kernel.FunctionInvoking -= MyRemovedPreExecutionHandler;

        kernel.FunctionInvoked += MyPostExecutionHandler;

        const string input = "I missed the F1 final race";

        var result = await kernel.RunAsync(input, excuseFunction);
        Console.WriteLine($"Function Result: {result}");
    }

    private static async Task ChangingResultAsync()
    {
        Console.WriteLine("\n======== Changing/Filtering Function Result ========\n");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAITextCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        const string functionPrompt = "Write a paragraph about Handlers.";

        var writerFunction = kernel.CreateSemanticFunction(
            functionPrompt,
            skillName: "MySkill",
            functionName: "Writer",
            maxTokens: 100,
            temperature: 0.4,
            topP: 1);

        void MyChangeDataHandler(object? sender, FunctionInvokedEventArgs e)
        {
            var originalOutput = e.SKContext.Result;

            //Use Regex to redact all vowels and numbers
            var newOutput = Regex.Replace(originalOutput, "[aeiouAEIOU0-9]", "*");

            e.SKContext.Variables.Update(newOutput);
        }

        kernel.FunctionInvoked += MyChangeDataHandler;

        var result = await kernel.RunAsync(writerFunction);

        Console.WriteLine($"Function Result: {result}");
    }

    private static async Task CancellingFunctionAsync()
    {
        Console.WriteLine("\n======== Cancelling Pipeline Execution ========\n");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAITextCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        const string functionPrompt = "Write a paragraph about: Cancellation.";

        var writerFunction = kernel.CreateSemanticFunction(
            functionPrompt,
            skillName: "MySkill",
            functionName: "Writer",
            maxTokens: 100,
            temperature: 0.4,
            topP: 1);

        // Adding new inline handler to cancel/prevent function execution
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            Console.WriteLine($"{e.FunctionView.SkillName}.{e.FunctionView.Name} : FunctionInvoking - Cancelling all subsequent invocations");
            e.Cancel();
        };

        // Technically invoked will never be called since the function will be cancelled
        int functionInvokedCount = 0;
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            functionInvokedCount++;
        };

        var result = await kernel.RunAsync(writerFunction);
        Console.WriteLine($"Function Invocation Times: {functionInvokedCount}");
    }

    private static async Task SkippingFunctionAsync()
    {
        Console.WriteLine("\n======== Skip Function in the Pipeline ========\n");

        IKernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAITextCompletionService(
               modelId: openAIModelId!,
               apiKey: openAIApiKey!)
           .Build();

        var skipMeFunction = kernel.CreateSemanticFunction("Write a paragraph about Skipping",
            skillName: "MySkill",
            functionName: "SkipMe");

        var dontSkipMeFunction = kernel.CreateSemanticFunction("Write a paragraph about Handlers",
            skillName: "MySkill",
            functionName: "DontSkipMe");

        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            if (e.FunctionView.Name == "SkipMe")
            {
                e.Skip();
                Console.WriteLine($"Function {e.FunctionView.Name} will be skipped");
                return;
            }

            Console.WriteLine($"Function {e.FunctionView.Name} will not be skipped");
        };

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            Console.WriteLine($"Only not skipped functions will trigger invoked event - Function name: {e.FunctionView.Name}");
        };

        var context = await kernel.RunAsync(
            skipMeFunction,
            dontSkipMeFunction);

        Console.WriteLine($"Final result: {context.Result}");
    }
}
