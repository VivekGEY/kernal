﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextGeneration;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using RepoUtils;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

/**
 * The following example shows how to plug into SK a custom text generation model.
 *
 * This might be useful in a few scenarios, for example:
 * - You are not using OpenAI or Azure OpenAI models
 * - You are using OpenAI/Azure OpenAI models but the models are behind a web service with a different API schema
 * - You want to use a local model
 *
 * Note that all text generation models are deprecated by OpenAI and will be removed in a future release.
 *
 * Refer to example 33 for streaming chat completion.
 */
// ReSharper disable StringLiteralTypo
// ReSharper disable once InconsistentNaming
public static class Example16_CustomLLM
{
    private const string LLMResultText = @" ..output from your custom model... Example:
    AI is awesome because it can help us solve complex problems, enhance our creativity,
    and improve our lives in many ways. AI can perform tasks that are too difficult,
    tedious, or dangerous for humans, such as diagnosing diseases, detecting fraud, or
    exploring space. AI can also augment our abilities and inspire us to create new forms
    of art, music, or literature. AI can also improve our well-being and happiness by
    providing personalized recommendations, entertainment, and assistance. AI is awesome";

    public static async Task RunAsync()
    {
        await CustomTextGenerationWithSKFunctionAsync();

        await CustomTextGenerationAsync();
        await CustomTextGenerationStreamAsync();
    }

    private static async Task CustomTextGenerationWithSKFunctionAsync()
    {
        Console.WriteLine("======== Custom LLM - Text Completion - SKFunction ========");

        Kernel kernel = new KernelBuilder().WithServices(c =>
        {
            c.AddSingleton(ConsoleLogger.LoggerFactory)
            // Add your text generation service as a singleton instance
            .AddKeyedSingleton<ITextGenerationService>("myService1", new MyTextGenerationService())
            // Add your text generation service as a factory method
            .AddKeyedSingleton<ITextGenerationService>("myService2", (_, _) => new MyTextGenerationService());
        }).Build();

        const string FunctionDefinition = "Does the text contain grammar errors (Y/N)? Text: {{$input}}";

        var textValidationFunction = kernel.CreateFunctionFromPrompt(FunctionDefinition);

        var result = await textValidationFunction.InvokeAsync(kernel, "I mised the training session this morning");
        Console.WriteLine(result.GetValue<string>());

        // Details of the my custom model response
        Console.WriteLine(JsonSerializer.Serialize(
            result.GetModelResults(),
            new JsonSerializerOptions() { WriteIndented = true }
        ));
    }

    private static async Task CustomTextGenerationAsync()
    {
        Console.WriteLine("======== Custom LLM  - Text Completion - Raw ========");
        var completionService = new MyTextGenerationService();

        var result = await completionService.CompleteAsync("I missed the training session this morning");

        Console.WriteLine(result);
    }

    private static async Task CustomTextGenerationStreamAsync()
    {
        Console.WriteLine("======== Custom LLM  - Text Completion - Raw Streaming ========");

        Kernel kernel = new KernelBuilder().WithLoggerFactory(ConsoleLogger.LoggerFactory).Build();
        ITextGenerationService textGeneration = new MyTextGenerationService();

        var prompt = "Write one paragraph why AI is awesome";
        await TextGenerationStreamAsync(prompt, textGeneration);
    }

    private static async Task TextGenerationStreamAsync(string prompt, ITextGenerationService textGeneration)
    {
        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            MaxTokens = 100,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Temperature = 1,
            TopP = 0.5
        };

        Console.WriteLine("Prompt: " + prompt);
        await foreach (var message in textGeneration.GetStreamingContentAsync(prompt, executionSettings))
        {
            Console.Write(message);
        }

        Console.WriteLine();
    }

    private sealed class MyTextGenerationService : ITextGenerationService
    {
        public string? ModelId { get; private set; }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(string prompt, PromptExecutionSettings? executionSettings, Kernel? kernel, CancellationToken cancellationToken = default)
        {
            this.ModelId = executionSettings?.ModelId;

            return Task.FromResult<IReadOnlyList<ITextResult>>(new List<ITextResult>
            {
                new MyTextGenerationStreamingResult()
            });
        }

        public async IAsyncEnumerable<T> GetStreamingContentAsync<T>(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (typeof(T) == typeof(MyStreamingContent) ||
                typeof(T) == typeof(StreamingContent))
            {
                foreach (string word in LLMResultText.Split(' '))
                {
                    await Task.Delay(50, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return (T)(object)new MyStreamingContent(word);
                }
            }
        }
    }

    private sealed class MyStreamingContent : StreamingContent
    {
        public override int ChoiceIndex => 0;

        public string Content { get; }

        public MyStreamingContent(string content) : base(content)
        {
            this.Content = $"{content} ";
        }

        public override byte[] ToByteArray()
        {
            return Encoding.UTF8.GetBytes(this.Content);
        }

        public override string ToString()
        {
            return this.Content;
        }
    }

    private sealed class MyTextGenerationStreamingResult : ITextResult
    {
        private readonly ModelResult _modelResult = new(new
        {
            Content = LLMResultText,
            Message = "This is my model raw response",
            Tokens = LLMResultText.Split(' ').Length
        });

        public ModelResult ModelResult => this._modelResult;

        public async Task<string> GetCompletionAsync(CancellationToken cancellationToken = default)
        {
            // Forcing a 1 sec delay (Simulating custom LLM lag)
            await Task.Delay(1000, cancellationToken);

            return LLMResultText;
        }
    }
}
