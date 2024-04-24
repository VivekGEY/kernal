﻿// Copyright (c) Microsoft. All rights reserved.
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

/// <summary>
/// Demonstrate creation of <see cref="OpenAIAssistantAgent"/> and
/// eliciting its response to three explicit user messages.
/// </summary>
/// <remarks>
/// This example demonstrates that outside of initialization (and cleanup), using
/// <see cref="OpenAIAssistantAgent"/> is no different from <see cref="ChatCompletionAgent"/>.
/// </remarks>
public class Example11_OpenAIAssistant(ITestOutputHelper output) : BaseTest(output)
{
    private const string ParrotName = "Parrot";
    private const string ParrotInstructions = "Repeat the user message in the voice of a pirate and then end with a parrot sound.";

    [Fact]
    public async Task RunAsync()
    {
        // Define the agent
        OpenAIAssistantAgent agent =
            await OpenAIAssistantAgent.CreateAsync(
                kernel: this.CreateEmptyKernel(),
                config: new(this.ApiKey, this.Endpoint),
                definition: new()
                {
                    Instructions = ParrotInstructions,
                    Name = ParrotName,
                    ModelId = this.Model,
                });

        // Create a chat for agent interaction.
        var chat = new AgentGroupChat();

        // Respond to user input
        try
        {
            await InvokeAgentAsync("Fortune favors the bold.");
            await InvokeAgentAsync("I came, I saw, I conquered.");
            await InvokeAgentAsync("Practice makes perfect.");
        }
        finally
        {
            await agent.DeleteAsync();
        }

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(string input)
        {
            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

            this.WriteLine($"# {AuthorRole.User}: '{input}'");

            await foreach (var content in chat.InvokeAsync(agent))
            {
                this.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
            }
        }
    }
}
