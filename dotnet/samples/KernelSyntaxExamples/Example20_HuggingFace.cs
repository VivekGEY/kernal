﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using RepoUtils;

/**
 * The following example shows how to use Semantic Kernel with HuggingFace API.
 */

// ReSharper disable once InconsistentNaming
public static class Example20_HuggingFace
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== HuggingFace text completion AI ========");

        IKernel kernel = new KernelBuilder()
            .WithLogger(ConsoleLogger.Logger)
            .WithHuggingFaceTextCompletionService(
                model: TestConfiguration.HuggingFace.ApiKey,
                apiKey: TestConfiguration.HuggingFace.ApiKey)
            .Build();

        const string FunctionDefinition = "Question: {{$input}}; Answer:";

        var questionAnswerFunction = kernel.CreateSemanticFunction(FunctionDefinition);

        var result = await kernel.RunAsync("What is New York?", questionAnswerFunction);

        Console.WriteLine(result);

        foreach (var modelResult in result.ModelResults)
        {
            Console.WriteLine(modelResult.GetHuggingFaceResult().AsJson());
        }
    }
}
