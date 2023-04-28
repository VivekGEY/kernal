﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.Pinecone;
using Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Model;
using Microsoft.SemanticKernel.Memory;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example28_Pinecone
{
    private const string MemoryCollectionName = "pinecone-test";

    public static async Task RunAsync()
    {
        string apiKey = Env.Var("PINECONE_API_KEY");
        PineconeEnvironment pineconeEnvironment = PineconeUtils.GetEnvironment(Env.Var("PINECONE_ENVIRONMENT"));
        IndexDefinition indexDefinition = IndexDefinition.Default(MemoryCollectionName);
        PineconeMemoryStore? memoryStore = await PineconeMemoryStore.InitializeAsync(pineconeEnvironment, apiKey, indexDefinition);

        IKernel kernel = Kernel.Builder
            .WithLogger(ConsoleLogger.Log)
            .Configure(c =>
            {
                c.AddOpenAITextCompletionService("davinci", "text-davinci-003", Env.Var("OPENAI_API_KEY"));
                c.AddOpenAITextEmbeddingGenerationService("ada", "text-embedding-ada-002", Env.Var("OPENAI_API_KEY"));
            })
            .WithMemoryStorage(memoryStore ?? throw new InvalidOperationException())
            .Build();

        Console.WriteLine("== Printing Collections in DB ==");
        IAsyncEnumerable<string> collections = memoryStore.GetCollectionsAsync();
        await foreach (string collection in collections)
        {
            Console.WriteLine(collection);
        }

        Console.WriteLine("== Adding Memories ==");
        Dictionary<string, object> metadata = new Dictionary<string, object>()
        {
            { "type", "text" },
            { "tags", new List<string>() { "memory", "cats" } }
        };

        string additionalMetadata = System.Text.Json.JsonSerializer.Serialize(metadata);
        string key1 = await kernel.Memory.SaveInformationAsync(MemoryCollectionName, "british short hair", "cat1", null, additionalMetadata);
        string key2 = await kernel.Memory.SaveInformationAsync(MemoryCollectionName, "orange tabby", "cat2", null, additionalMetadata);
        string key3 = await kernel.Memory.SaveInformationAsync(MemoryCollectionName, "norwegian forest cat", "cat3", null, additionalMetadata);

        Console.WriteLine("== Printing Collections in DB ==");
        collections = memoryStore.GetCollectionsAsync();
        await foreach (string collection in collections)
        {
            Console.WriteLine(collection);
        }

        Console.WriteLine("== Retrieving Memories Through the Kernel ==");
        MemoryQueryResult? lookup = await kernel.Memory.GetAsync(MemoryCollectionName, "cat1");
        Console.WriteLine(lookup != null ? lookup.Metadata.Text : "ERROR: memory not found");

        Console.WriteLine("== Retrieving Memories Directly From the Store ==");
        var memory1 = await memoryStore.GetAsync(MemoryCollectionName, key1);
        var memory2 = await memoryStore.GetAsync(MemoryCollectionName, key2);
        var memory3 = await memoryStore.GetAsync(MemoryCollectionName, key3);
        Console.WriteLine(memory1 != null ? memory1.Metadata.Text : "ERROR: memory not found");
        Console.WriteLine(memory2 != null ? memory2.Metadata.Text : "ERROR: memory not found");
        Console.WriteLine(memory3 != null ? memory3.Metadata.Text : "ERROR: memory not found");

        Console.WriteLine("== Similarity Searching Memories: My favorite color is orange ==");
        IAsyncEnumerable<MemoryQueryResult> searchResults = kernel.Memory.SearchAsync(MemoryCollectionName, "My favorite color is orange", 1, 0.8);

        await foreach (MemoryQueryResult item in searchResults)
        {
            Console.WriteLine(item.Metadata.Text + " : " + item.Relevance);
        }

        Console.WriteLine("== Removing Collection {0} ==", MemoryCollectionName);
        await memoryStore.DeleteCollectionAsync(MemoryCollectionName);
        //
        Console.WriteLine("== Printing Collections in DB ==");
        await foreach (string collection in collections)
        {
            Console.WriteLine(collection);
        }
    }
}
