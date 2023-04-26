﻿// Copyright (c) Microsoft. All rights reserved.

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning.Planners;
using Microsoft.SemanticKernel.SkillDefinition;
using SemanticKernel.Service.Config;
using SemanticKernel.Service.Storage;

namespace SemanticKernel.Service.Skills;



/// <summary>
/// ChatSkill offers a more coherent chat experience by using memories
/// to extract conversation history and user intentions.
/// </summary>
public class ChatSkill
{
    /// <summary>
    /// Logger
    /// </summary>
    private readonly ILogger _logger;
    
    /// <summary>
    /// A kernel instance to create a completion function since each invocation
    /// of the <see cref="ChatAsync"/> function will generate a new prompt dynamically.
    /// </summary>
    private readonly IKernel _kernel;

    /// <summary>
    /// A repository to save and retrieve chat messages.
    /// </summary>
    private readonly ChatMessageRepository _chatMessageRepository;

    /// <summary>
    /// A repository to save and retrieve chat sessions.
    /// </summary>
    private readonly ChatSessionRepository _chatSessionRepository;

    /// <summary>
    /// Settings containing prompt texts.
    /// </summary>
    private readonly PromptSettings _promptSettings;

    /// <summary>
    /// A planner to gather additional information for the user.
    /// </summary>
    private readonly SequentialPlanner _planner;

    /// <summary>
    /// Options for the planner.
    /// </summary>
    private readonly PlannerOptions _plannerOptions;

    /// <summary>
    /// Create a new instance of <see cref="ChatSkill"/>.
    /// </summary>
    public ChatSkill(
        IKernel kernel,
        ChatMessageRepository chatMessageRepository,
        ChatSessionRepository chatSessionRepository,
        PromptSettings promptSettings,
        SequentialPlanner planner,
        PlannerOptions plannerOptions,
        ILogger logger)
    {
        this._logger = logger;
        this._kernel = kernel;
        this._chatMessageRepository = chatMessageRepository;
        this._chatSessionRepository = chatSessionRepository;
        this._promptSettings = promptSettings;
        this._planner = planner;
        this._plannerOptions = plannerOptions;
    }

    /// <summary>
    /// Extract user intent from the conversation history.
    /// </summary>
    /// <param name="context">Contains the 'audience' indicating the name of the user.</param>
    [SKFunction("Extract user intent")]
    [SKFunctionName("ExtractUserIntent")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Chat ID to extract history from")]
    [SKFunctionContextParameter(Name = "audience", Description = "The audience the chat bot is interacting with.")]
    public async Task<string> ExtractUserIntentAsync(SKContext context)
    {
        var tokenLimit = this._promptSettings.CompletionTokenLimit;
        var historyTokenBudget =
            tokenLimit -
            this._promptSettings.ResponseTokenLimit -
            Utils.TokenCount(string.Join("\n", new string[]
                {
                    this._promptSettings.SystemDescriptionPrompt,
                    this._promptSettings.SystemIntentPrompt,
                    this._promptSettings.SystemIntentContinuationPrompt
                })
            );

        // Clone the context to avoid modifying the original context variables.
        var intentExtractionContext = Utils.CopyContextWithVariablesClone(context);
        intentExtractionContext.Variables.Set("tokenLimit", historyTokenBudget.ToString(new NumberFormatInfo()));
        intentExtractionContext.Variables.Set("knowledgeCutoff", this._promptSettings.KnowledgeCutoffDate);

        var completionFunction = this._kernel.CreateSemanticFunction(
            this._promptSettings.SystemIntentExtractionPrompt,
            skillName: nameof(ChatSkill),
            description: "Complete the prompt.");

        var result = await completionFunction.InvokeAsync(
            intentExtractionContext,
            settings: this.CreateIntentCompletionSettings()
        );

        if (result.ErrorOccurred)
        {
            context.Fail(result.LastErrorDescription, result.LastException);
            return string.Empty;
        }

        return $"User intent: {result}";
    }

    /// <summary>
    /// Extract relevant memories based on the latest message.
    /// </summary>
    /// <param name="context">Contains the 'tokenLimit' and the 'contextTokenLimit' controlling the length of the prompt.</param>
    [SKFunction("Extract user memories")]
    [SKFunctionName("ExtractUserMemories")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Chat ID to extract history from")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens")]
    [SKFunctionContextParameter(Name = "contextTokenLimit", Description = "Maximum number of context tokens")]
    public async Task<string> ExtractUserMemoriesAsync(SKContext context)
    {
        var chatId = context["chatId"];
        var tokenLimit = int.Parse(context["tokenLimit"], new NumberFormatInfo());
        var contextTokenLimit = int.Parse(context["contextTokenLimit"], new NumberFormatInfo());
        var remainingToken = Math.Min(
            tokenLimit,
            Math.Floor(contextTokenLimit * this._promptSettings.MemoriesResponseContextWeight)
        );

        // Find the most recent message.
        var latestMessage = await this._chatMessageRepository.FindLastByChatIdAsync(chatId);

        // Search for relevant memories.
        List<MemoryQueryResult> relevantMemories = new();
        foreach (var memoryName in this._promptSettings.MemoryMap.Keys)
        {
            var results = context.Memory.SearchAsync(
                SemanticMemoryExtractor.MemoryCollectionName(chatId, memoryName),
                latestMessage.ToString(),
                limit: 100,
                minRelevanceScore: 0.8);
            await foreach (var memory in results)
            {
                relevantMemories.Add(memory);
            }
        }

        relevantMemories = relevantMemories.OrderByDescending(m => m.Relevance).ToList();

        string memoryText = "";
        foreach (var memory in relevantMemories)
        {
            var tokenCount = Utils.TokenCount(memory.Metadata.Text);
            if (remainingToken - tokenCount > 0)
            {
                memoryText += $"\n[{memory.Metadata.Description}] {memory.Metadata.Text}";
                remainingToken -= tokenCount;
            }
            else
            {
                break;
            }
        }

        // Update the token limit.
        memoryText = $"Past memories (format: [memory type] <label>: <details>):\n{memoryText.Trim()}";
        tokenLimit -= Utils.TokenCount(memoryText);
        context.Variables.Set("tokenLimit", tokenLimit.ToString(new NumberFormatInfo()));

        return memoryText;
    }

    /// <summary>
    /// Extract relevant additional knowledge.
    /// </summary>
    [SKFunction("Acquire external information")]
    [SKFunctionName("AcquireExternalInformation")]
    [SKFunctionContextParameter(Name = "userIntent", Description = "The intent of the user.")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens")]
    public async Task<string> AcquireExternalInformationAsync(SKContext context)
    {
        int tokenLimit = int.Parse(context["tokenLimit"], new NumberFormatInfo());
        string userIntent = context["userIntent"];

        // Create a new kernel with only skills we want the planner to use and keep the rest of the current context.
        using Kernel plannerKernel = new(
            new SkillCollection(),
            this._kernel.PromptTemplateEngine,
            this._kernel.Memory,
            this._kernel.Config,
            this._kernel.Log);

        // Import skills into the planner's kernel.
        await plannerKernel.ImportChatGptPluginSkillFromUrlAsync("Klarna", new Uri("https://www.klarna.com/.well-known/ai-plugin.json")); // Klarna 

        // Create the planner
        SequentialPlanner planner = new(
            kernel: plannerKernel,
            config: this._plannerOptions.ToPlannerConfig());

        // Create a plan and run it.
        Plan plan = await planner.CreatePlanAsync(userIntent);
        while (plan.HasNextStep)
        {
            var nextStep = plan.Steps[plan.NextStepIndex];
            if (nextStep.SkillName.Contains(".Plan", StringComparison.InvariantCultureIgnoreCase) && nextStep.Steps.Count == 0)
            {
                this._logger.LogWarning("Planner created step that is a plan with no steps. This should not happen.");
                break;
            }
            else
            {
                await plannerKernel.StepAsync(context.Variables, plan);
            }
        }

        // The result of the plan is likely from an OpenAPI-based skill - extract the JSON from the response.
        // Otherwise, just use result of the plan execution directly.
        if (!this.TryExtractJsonFromOpenApiResponse(plan.State.Input, out string planResult))
        {
            planResult = plan.State.Input;
        }

        // TODO add source/citations

        string informationText = $"[START RELATED INFORMATION]\n{planResult.Trim()}\n[END RELATED INFORMATION]\n";

        tokenLimit -= Utils.TokenCount(informationText);

        context.Variables.Set("tokenLimit", tokenLimit.ToString(new NumberFormatInfo()));

        return informationText;
    }

    /// <summary>
    /// Extract JSON from an OpenAPI skill response.
    /// </summary>
    private bool TryExtractJsonFromOpenApiResponse(string openApiSkillResponse, out string json)
    {
        JsonNode? jsonNode = JsonNode.Parse(openApiSkillResponse);
        string contentType = jsonNode?["contentType"]?.ToString() ?? string.Empty;
        if (contentType.StartsWith("application/json", StringComparison.InvariantCultureIgnoreCase))
        {
            var content = jsonNode?["content"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(content))
            {
                json = content;
                return true;
            }
        }

        json = string.Empty;
        return false;
    }
   
    /// <summary>
    /// Extract chat history.
    /// </summary>
    /// <param name="context">Contains the 'tokenLimit' controlling the length of the prompt.</param>
    [SKFunction("Extract chat history")]
    [SKFunctionName("ExtractChatHistory")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Chat ID to extract history from")]
    [SKFunctionContextParameter(Name = "tokenLimit", Description = "Maximum number of tokens")]
    public async Task<string> ExtractChatHistoryAsync(SKContext context)
    {
        var chatId = context["chatId"];
        var tokenLimit = int.Parse(context["tokenLimit"], new NumberFormatInfo());

        var messages = await this._chatMessageRepository.FindByChatIdAsync(chatId);
        var sortedMessages = messages.OrderByDescending(m => m.Timestamp);

        var remainingToken = tokenLimit;
        string historyText = "";
        foreach (var chatMessage in sortedMessages)
        {
            var formattedMessage = chatMessage.ToFormattedString();
            var tokenCount = Utils.TokenCount(formattedMessage);
            if (remainingToken - tokenCount > 0)
            {
                historyText = $"{formattedMessage}\n{historyText}";
                remainingToken -= tokenCount;
            }
            else
            {
                break;
            }
        }

        return $"Chat history:\n{historyText.Trim()}";
    }

    /// <summary>
    /// This is the entry point for getting a chat response. It manages the token limit, saves
    /// messages to memory, and fill in the necessary context variables for completing the
    /// prompt that will be rendered by the template engine.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="context">Contains the 'tokenLimit' and the 'contextTokenLimit' controlling the length of the prompt.</param>
    [SKFunction("Get chat response")]
    [SKFunctionName("Chat")]
    [SKFunctionInput(Description = "The new message")]
    [SKFunctionContextParameter(Name = "userId", Description = "Unique and persistent identifier for the user")]
    [SKFunctionContextParameter(Name = "userName", Description = "Name of the user")]
    [SKFunctionContextParameter(Name = "chatId", Description = "Unique and persistent identifier for the chat")]
    public async Task<SKContext> ChatAsync(string message, SKContext context)
    {
        var tokenLimit = this._promptSettings.CompletionTokenLimit;
        var remainingToken =
            tokenLimit -
            this._promptSettings.ResponseTokenLimit -
            Utils.TokenCount(string.Join("\n", new string[]
                {
                    this._promptSettings.SystemDescriptionPrompt,
                    this._promptSettings.SystemResponsePrompt,
                    this._promptSettings.SystemChatContinuationPrompt
                })
            );
        var contextTokenLimit = remainingToken;
        var userId = context["userId"];
        var userName = context["userName"];
        var chatId = context["chatId"];

        // TODO: check if user has access to the chat

        // Save this new message to memory such that subsequent chat responses can use it
        try
        {
            await this.SaveNewMessageAsync(message, userId, userName, chatId);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            context.Log.LogError("Unable to save new message: {0}", ex.Message);
            context.Fail($"Unable to save new message: {ex.Message}", ex);
            return context;
        }

        // Clone the context to avoid modifying the original context variables.
        SKContext chatContext = Utils.CopyContextWithVariablesClone(context);
        chatContext.Variables.Set("knowledgeCutoff", this._promptSettings.KnowledgeCutoffDate);
        chatContext.Variables.Set("audience", userName);

        // Extract user intent and update remaining token count
        var userIntent = await this.ExtractUserIntentAsync(chatContext);
        if (chatContext.ErrorOccurred)
        {
            return chatContext;
        }

        chatContext.Variables.Set("userIntent", userIntent);
        // Update remaining token count
        remainingToken -= Utils.TokenCount(userIntent);
        chatContext.Variables.Set("contextTokenLimit", contextTokenLimit.ToString(new NumberFormatInfo()));
        chatContext.Variables.Set("tokenLimit", remainingToken.ToString(new NumberFormatInfo()));

        var completionFunction = this._kernel.CreateSemanticFunction(
            this._promptSettings.SystemChatPrompt,
            skillName: nameof(ChatSkill),
            description: "Complete the prompt.");

        chatContext = await completionFunction.InvokeAsync(
            context: chatContext,
            settings: this.CreateChatResponseCompletionSettings()
        );

        // If the completion function failed, return the context containing the error.
        if (chatContext.ErrorOccurred)
        {
            return chatContext;
        }

        // Save this response to memory such that subsequent chat responses can use it
        try
        {
            await this.SaveNewResponseAsync(chatContext.Result, chatId);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            context.Log.LogError("Unable to save new response: {0}", ex.Message);
            context.Fail($"Unable to save new response: {ex.Message}", ex);
            return context;
        }

        // Extract semantic memory
        await this.ExtractSemanticMemoryAsync(chatId, chatContext);

        context.Variables.Update(chatContext.Result);
        context.Variables.Set("userId", "Bot");
        return context;
    }

    #region Private

    /// <summary>
    /// Save a new message to the chat history.
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="userId">The user ID</param>
    /// <param name="userName"></param>
    /// <param name="chatId">The chat ID</param>
    private async Task SaveNewMessageAsync(string message, string userId, string userName, string chatId)
    {
        // Make sure the chat exists.
        await this._chatSessionRepository.FindByIdAsync(chatId);

        var chatMessage = new ChatMessage(userId, userName, chatId, message);
        await this._chatMessageRepository.CreateAsync(chatMessage);
    }

    /// <summary>
    /// Save a new response to the chat history.
    /// </summary>
    /// <param name="response">Response from the chat.</param>
    /// <param name="chatId">The chat ID</param>
    private async Task SaveNewResponseAsync(string response, string chatId)
    {
        // Make sure the chat exists.
        await this._chatSessionRepository.FindByIdAsync(chatId);

        var chatMessage = ChatMessage.CreateBotResponseMessage(chatId, response);
        await this._chatMessageRepository.CreateAsync(chatMessage);
    }

    /// <summary>
    /// Extract and save semantic memory.
    /// </summary>
    /// <param name="chatId">The Chat ID.</param>
    /// <param name="context">The context containing the memory.</param>
    private async Task ExtractSemanticMemoryAsync(string chatId, SKContext context)
    {
        foreach (var memoryName in this._promptSettings.MemoryMap.Keys)
        {
            try
            {
                var semanticMemory = await SemanticMemoryExtractor.ExtractCognitiveMemoryAsync(
                    memoryName,
                    this._kernel,
                    context,
                    this._promptSettings
                );
                foreach (var item in semanticMemory.Items)
                {
                    await this.CreateMemoryAsync(item, chatId, context, memoryName);
                }
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                // Skip semantic memory extraction for this item if it fails.
                // We cannot rely on the model to response with perfect Json each time.
                context.Log.LogInformation("Unable to extract semantic memory for {0}: {1}. Continuing...", memoryName, ex.Message);
                continue;
            }
        }
    }

    /// <summary>
    /// Create a memory item in the memory collection.
    /// </summary>
    /// <param name="item">A SemanticChatMemoryItem instance</param>
    /// <param name="chatId">The ID of the chat the memories belong to</param>
    /// <param name="context">The context that contains the memory</param>
    /// <param name="memoryName">Name of the memory</param>
    private async Task CreateMemoryAsync(SemanticChatMemoryItem item, string chatId, SKContext context, string memoryName)
    {
        var memoryCollectionName = SemanticMemoryExtractor.MemoryCollectionName(chatId, memoryName);

        var memories = context.Memory.SearchAsync(
            collection: memoryCollectionName,
            query: item.ToFormattedString(),
            limit: 1,
            minRelevanceScore: 0.8,
            cancel: context.CancellationToken
        ).ToEnumerable();

        if (!memories.Any())
        {
            await context.Memory.SaveInformationAsync(
                collection: memoryCollectionName,
                text: item.ToFormattedString(),
                id: Guid.NewGuid().ToString(),
                description: memoryName,
                cancel: context.CancellationToken
            );
        }
    }

    /// <summary>
    /// Create a completion settings object for chat response. Parameters are read from the PromptSettings class.
    /// </summary>
    private CompleteRequestSettings CreateChatResponseCompletionSettings()
    {
        var completionSettings = new CompleteRequestSettings
        {
            MaxTokens = this._promptSettings.ResponseTokenLimit,
            Temperature = this._promptSettings.ResponseTemperature,
            TopP = this._promptSettings.ResponseTopP,
            FrequencyPenalty = this._promptSettings.ResponseFrequencyPenalty,
            PresencePenalty = this._promptSettings.ResponsePresencePenalty
        };

        return completionSettings;
    }

    /// <summary>
    /// Create a completion settings object for intent response. Parameters are read from the PromptSettings class.
    /// </summary>
    private CompleteRequestSettings CreateIntentCompletionSettings()
    {
        var completionSettings = new CompleteRequestSettings
        {
            MaxTokens = this._promptSettings.ResponseTokenLimit,
            Temperature = this._promptSettings.IntentTemperature,
            TopP = this._promptSettings.IntentTopP,
            FrequencyPenalty = this._promptSettings.IntentFrequencyPenalty,
            PresencePenalty = this._promptSettings.IntentPresencePenalty,
            StopSequences = new string[] { "] bot:" }
        };

        return completionSettings;
    }

    # endregion
}
