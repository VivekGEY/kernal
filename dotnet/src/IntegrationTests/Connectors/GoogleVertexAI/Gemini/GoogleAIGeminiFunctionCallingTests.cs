﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.GoogleVertexAI;
using xRetry;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.GoogleVertexAI.Gemini;

public sealed class GeminiFunctionCallingTests
{
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    private readonly ITestOutputHelper _output;

    public GeminiFunctionCallingTests(ITestOutputHelper output)
    {
        this._output = output;
    }

    // [RetryFact(Skip = "This test is for manual verification.")]
    [RetryFact]
    public async Task EnabledFunctionsShouldReturnFunctionToCallAsync()
    {
        // Arrange
        var kernel = new Kernel();
        kernel.ImportPluginFromType<CustomerPlugin>(nameof(CustomerPlugin));
        var sut = new GoogleAIGeminiChatCompletionService(this.GetModel(), this.GetApiKey());
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello, could you show me list of customers?");
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000,
            ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions,
        };

        // Act
        var response = await sut.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

        // Assert
        var geminiResponse = response as GeminiChatMessageContent;
        Assert.NotNull(geminiResponse);
        Assert.NotNull(geminiResponse.ToolCalls);
        Assert.Single(geminiResponse.ToolCalls, item =>
            item.FullyQualifiedName == $"{nameof(CustomerPlugin)}{GeminiFunction.NameSeparator}{nameof(CustomerPlugin.GetCustomers)}");
    }

    // [RetryFact(Skip = "This test is for manual verification.")]
    [RetryFact]
    public async Task AutoInvokeShouldCallOneFunctionAndReturnResponseAsync()
    {
        // Arrange
        var kernel = new Kernel();
        kernel.ImportPluginFromType<CustomerPlugin>("CustomerPlugin");
        var sut = new GoogleAIGeminiChatCompletionService(this.GetModel(), this.GetApiKey());
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello, could you show me list of customers?");
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // Act
        var response = await sut.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

        // Assert
        this._output.WriteLine(response.Content);
        Assert.Contains("John Kowalski", response.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Anna Nowak", response.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Steve Smith", response.Content, StringComparison.OrdinalIgnoreCase);
    }

    // [RetryFact(Skip = "This test is for manual verification.")]
    [RetryFact]
    public async Task AutoInvokeShouldCallTwoFunctionsAndReturnResponseAsync()
    {
        // Arrange
        var kernel = new Kernel();
        kernel.ImportPluginFromType<CustomerPlugin>("CustomerPlugin");
        var sut = new GoogleAIGeminiChatCompletionService(this.GetModel(), this.GetApiKey());
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello, could you show me list of customers first and next return age of Anna customer?");
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // Act
        var response = await sut.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

        // Assert
        this._output.WriteLine(response.Content);
        Assert.Contains("28", response.Content, StringComparison.OrdinalIgnoreCase);
    }

    // [RetryFact(Skip = "This test is for manual verification.")]
    [RetryFact]
    public async Task AutoInvokeShouldCallFunctionsMultipleTimesAndReturnResponseAsync()
    {
        // Arrange
        var kernel = new Kernel();
        kernel.ImportPluginFromType<CustomerPlugin>("CustomerPlugin");
        kernel.ImportPluginFromType<MathPlugin>("MathPlugin");
        var sut = new GoogleAIGeminiChatCompletionService(this.GetModel(), this.GetApiKey());
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(
            "Get list of customers and next get customers ages and at the end calculate the sum of ages of all customers.");
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // Act
        var response = await sut.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

        // Assert
        this._output.WriteLine(response.Content);
        Assert.Contains("105", response.Content, StringComparison.OrdinalIgnoreCase);
    }

    // [RetryFact(Skip = "This test is for manual verification.")]
    [RetryFact]
    public async Task AutoInvokeTwoPluginsShouldGetDateAndReturnTasksByDateParamAndReturnResponseAsync()
    {
        // Arrange
        var kernel = new Kernel();
        kernel.ImportPluginFromType<TaskPlugin>(nameof(TaskPlugin));
        kernel.ImportPluginFromType<DatePlugin>(nameof(DatePlugin));
        var sut = new GoogleAIGeminiChatCompletionService(this.GetModel(), this.GetApiKey());
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("How many tasks I have to do today? Show me count of tasks for today and date.");
        var executionSettings = new GeminiPromptExecutionSettings()
        {
            MaxTokens = 2000,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // Act
        var response = await sut.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

        // Assert
        this._output.WriteLine(response.Content);
        Assert.Contains("5", response.Content, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class CustomerPlugin
    {
        [KernelFunction(nameof(GetCustomers))]
        [Description("Get list of customers.")]
        [return: Description("List of customers.")]
        public string[] GetCustomers()
        {
            return new[]
            {
                "John Kowalski",
                "Anna Nowak",
                "Steve Smith",
            };
        }

        [KernelFunction(nameof(GetCustomerAge))]
        [Description("Get age of customer.")]
        [return: Description("Age of customer.")]
        public int GetCustomerAge([Description("Name of customer")] string customerName)
        {
            return customerName switch
            {
                "John Kowalski" => 35,
                "Anna Nowak" => 28,
                "Steve Smith" => 42,
                _ => throw new ArgumentException("Customer not found."),
            };
        }
    }

    public sealed class TaskPlugin
    {
        [KernelFunction(nameof(GetTaskCount))]
        [Description("Get count of tasks for specific date.")]
        public int GetTaskCount([Description("Date to get tasks")] DateTime date)
        {
            return 5;
        }
    }

    public sealed class DatePlugin
    {
        [KernelFunction(nameof(GetDate))]
        [Description("Get current (today) date.")]
        public DateTime GetDate()
        {
            return DateTime.Now.Date;
        }
    }

    public sealed class MathPlugin
    {
        [KernelFunction(nameof(Sum))]
        [Description("Sum numbers.")]
        public int Sum([Description("Numbers to sum")] int[] numbers)
        {
            return numbers.Sum();
        }
    }

    private string GetModel() => this._configuration.GetSection("GoogleAI:Gemini:ModelId").Get<string>()!;
    private string GetApiKey() => this._configuration.GetSection("GoogleAI:ApiKey").Get<string>()!;
}
