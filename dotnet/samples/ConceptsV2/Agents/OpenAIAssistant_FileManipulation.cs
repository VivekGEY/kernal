﻿// Copyright (c) Microsoft. All rights reserved.
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using OpenAI.Files;
using Resources;

namespace Agents;

/// <summary>
/// Demonstrate using code-interpreter to manipulate and generate csv files with <see cref="OpenAIAssistantAgent"/> .
/// </summary>
public class OpenAIAssistant_FileManipulation(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// Target OpenAI services.
    /// </summary>
    protected override bool ForceOpenAI => true;

    [Fact]
    public async Task AnalyzeCSVFileUsingOpenAIAssistantAgentAsync()
    {
        OpenAIClient rootClient = OpenAIClientFactory.CreateClient(GetOpenAIConfiguration()); // %%% HACK
        FileClient fileClient = rootClient.GetFileClient();

        await using Stream fileStream = EmbeddedResource.ReadStream("sales.csv")!;
        OpenAIFileInfo fileInfo =
            await fileClient.UploadFileAsync(
                    fileStream,
                    "sales.csv",
                    FileUploadPurpose.Assistants);

        //OpenAIFileService fileService = new(TestConfiguration.OpenAI.ApiKey); // %%% USE THIS
        //OpenAIFileReference uploadFile =
        //    await fileService.UploadContentAsync(
        //        new BinaryContent(await EmbeddedResource.ReadAllAsync("sales.csv"), mimeType: "text/plain"),
        //        new OpenAIFileUploadExecutionSettings("sales.csv", OpenAIFilePurpose.Assistants));

        // Define the agent
        OpenAIAssistantAgent agent =
            await OpenAIAssistantAgent.CreateAsync(
                kernel: new(),
                config: GetOpenAIConfiguration(),
                new()
                {
                    EnableCodeInterpreter = true, // Enable code-interpreter
                    ModelName = this.Model,
                });

        // Create a chat for agent interaction.
        AgentGroupChat chat = new();

        // Respond to user input
        try
        {
            await InvokeAgentAsync("Which segment had the most sales?");
            await InvokeAgentAsync("List the top 5 countries that generated the most profit.");
            await InvokeAgentAsync("Create a tab delimited file report of profit by each country per month.");
        }
        finally
        {
            await agent.DeleteAsync();
            //await fileService.DeleteFileAsync(uploadFile.Id); // %%% USE THIS
            await fileClient.DeleteFileAsync(fileInfo.Id); // %%% HACK
        }

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(string input)
        {
            chat.AddChatMessage(
                new(AuthorRole.User, content: null)
                {
                    Items = [new TextContent(input), new FileReferenceContent(fileInfo.Id)]
                });

            Console.WriteLine($"# {AuthorRole.User}: '{input}'");

            await foreach (ChatMessageContent message in chat.InvokeAsync(agent))
            {
                Console.WriteLine($"# {message.Role} - {message.AuthorName ?? "*"}: '{message.Content}'");

                foreach (AnnotationContent annotation in message.Items.OfType<AnnotationContent>())
                {
                    Console.WriteLine($"\n* '{annotation.Quote}' => {annotation.FileId}"); // %%% HACK
                    BinaryData fileData = await fileClient.DownloadFileAsync(annotation.FileId!);
                    Console.WriteLine(Encoding.Default.GetString(fileData.ToArray()));
                    //BinaryContent fileContent = await fileService.GetFileContentAsync(annotation.FileId!); // %%% USE THIS
                    //byte[] byteContent = fileContent.Data?.ToArray() ?? [];
                    //Console.WriteLine(Encoding.Default.GetString(byteContent));
                }
            }
        }
    }

    private OpenAIConfiguration GetOpenAIConfiguration()
        =>
            this.UseOpenAIConfig ?
                OpenAIConfiguration.ForOpenAI(this.ApiKey) :
                OpenAIConfiguration.ForAzureOpenAI(this.ApiKey, new Uri(this.Endpoint!));
}