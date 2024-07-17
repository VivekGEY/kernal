﻿// See https://aka.ms/new-console-template for more information

using Connectors.Amazon.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;

// ----------------------------CHAT COMPLETION----------------------------
string userInput;
ChatHistory chatHistory = new ChatHistory();
do
{
    Console.Write("Enter a prompt (or 'exit' to quit): ");
    userInput = Console.ReadLine();

    if (userInput.ToLower() != "exit")
    {
        chatHistory.AddMessage(AuthorRole.User, userInput);

        // var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("amazon.titan-text-premier-v1:0").Build();
        // var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("anthropic.claude-3-sonnet-20240229-v1:0").Build();
        // var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("anthropic.claude-3-haiku-20240307-v1:0").Build();
        // var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("anthropic.claude-v2:1").Build();
        // var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("ai21.jamba-instruct-v1:0").Build();
        var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("cohere.command-r-plus-v1:0").Build(); //FAILING
        // var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("meta.llama3-8b-instruct-v1:0").Build();
        // var kernel = Kernel.CreateBuilder().AddBedrockChatCompletionService("mistral.mistral-7b-instruct-v0:2").Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletionService.GetChatMessageContentsAsync(chatHistory).ConfigureAwait(false);

        foreach (var message in result)
        {
            chatHistory.AddMessage(AuthorRole.Assistant, message.Content);
            Console.WriteLine($"Chat Completion Answer: {message.Content}");
            Console.WriteLine();
        }
    }
} while (userInput.ToLower() != "exit");




// ----------------------------TEXT GENERATION----------------------------
// Console.Write("Text Generation Prompt: ");
// string UserPrompt2 = Console.ReadLine();
// // var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("amazon.titan-text-premier-v1:0").Build();
// // var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("mistral.mistral-7b-instruct-v0:2").Build();
// // var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("ai21.jamba-instruct-v1:0").Build();
// // var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("anthropic.claude-v2:1").Build();
// // var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("cohere.command-text-v14").Build();
// // var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("meta.llama3-8b-instruct-v1:0").Build();
// var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("cohere.command-r-plus-v1:0").Build();
// // var kernel2 = Kernel.CreateBuilder().AddBedrockTextGenerationService("ai21.j2-ultra-v1").Build();
//
// var textGenerationService = kernel2.GetRequiredService<ITextGenerationService>();
// var textGeneration = await textGenerationService.GetTextContentsAsync(UserPrompt2).ConfigureAwait(false);
// if (textGeneration.Count > 0)
// {
//     var firstTextContent = textGeneration.FirstOrDefault();
//     if (firstTextContent != null)
//     {
//         Console.WriteLine("Text Generation Answer: " + firstTextContent.Text);
//     }
//     else
//     {
//         Console.WriteLine("Text Generation Answer: (none)");
//     }
// }
// else
// {
//     Console.WriteLine("Text Generation Answer: (No output text)");
// }





// ----------------------------STREAM TEXT GENERATION----------------------------
// Console.Write("Stream Text Generation Prompt: ");
// string UserPrompt3 = Console.ReadLine();
// // var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("amazon.titan-text-premier-v1:0").Build();
// // var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("anthropic.claude-v2").Build();
// // var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("mistral.mistral-7b-instruct-v0:2").Build();
// // var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("cohere.command-text-v14").Build();
// // var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("cohere.command-r-plus-v1:0").Build();
// var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("meta.llama3-8b-instruct-v1:0").Build();
//
// // AI21Labs Jurassic and Jamba models do not support streaming
// // var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("ai21.j2-ultra-v1").Build();
// // var kernel3 = Kernel.CreateBuilder().AddBedrockTextGenerationService("ai21.jamba-instruct-v1:0").Build();
//
// var streamTextGenerationService = kernel3.GetRequiredService<ITextGenerationService>();
// var streamTextGeneration = streamTextGenerationService.GetStreamingTextContentsAsync(UserPrompt3).ConfigureAwait(true);
// await foreach (var textContent in streamTextGeneration)
// {
//     Console.Write(textContent.Text);
// }






// ----------------------------STREAM CHAT COMPLETION----------------------------
// string userInput;
// ChatHistory chatHistory = new ChatHistory();
// do
// {
//     Console.Write("Enter a prompt (or 'exit' to quit): ");
//     userInput = Console.ReadLine();
//
//     if (userInput.ToLower() != "exit")
//     {
//         chatHistory.AddMessage(AuthorRole.User, userInput);
//         // Console.WriteLine($"Stream Chat Completion Question: {userInput}");
//
//         // var kernel4 = Kernel.CreateBuilder().AddBedrockChatCompletionService("mistral.mistral-7b-instruct-v0:2").Build();
//         // var kernel4 = Kernel.CreateBuilder().AddBedrockChatCompletionService("amazon.titan-text-premier-v1:0").Build();
//         // var kernel4 = Kernel.CreateBuilder().AddBedrockChatCompletionService("anthropic.claude-v2").Build();
//         // var kernel4 = Kernel.CreateBuilder().AddBedrockChatCompletionService("anthropic.claude-3-sonnet-20240229-v1:0").Build();
//         // var kernel4 = Kernel.CreateBuilder().AddBedrockChatCompletionService("cohere.command-r-plus-v1:0").Build(); //FAILING
//         var kernel4 = Kernel.CreateBuilder().AddBedrockChatCompletionService("meta.llama3-8b-instruct-v1:0").Build();
//
//         // AI21 Labs does not support streaming
//         // var kernel4 = Kernel.CreateBuilder().AddBedrockChatCompletionService("ai21.jamba-instruct-v1:0").Build();
//
//
//
//         var chatCompletionService = kernel4.GetRequiredService<IChatCompletionService>();
//         var result = chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory).ConfigureAwait(false);
//         string output = "";
//         await foreach (var message in result)
//         {
//             Console.Write($"{message.Content}");
//             output += message.Content;
//         }
//         Console.WriteLine();
//         chatHistory.AddMessage(AuthorRole.Assistant, output);
//     }
// } while (userInput.ToLower() != "exit");
