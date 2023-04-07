﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.CoreSkills;

public sealed class PlannerSkillTests : IDisposable
{
    public PlannerSkillTests(ITestOutputHelper output)
    {
        this._logger = new XunitLogger<object>(output);
        this._testOutputHelper = new RedirectOutput(output);

        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<PlannerSkillTests>()
            .Build();
    }

    [Theory]
    [InlineData("Write a poem or joke and send it in an e-mail to Kai.", "function._GLOBAL_FUNCTIONS_.SendEmail")]
    public async Task CreatePlanWithEmbeddingsTestAsync(string prompt, string expectedAnswerContains)
    {
        // Arrange
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        AzureOpenAIConfiguration? azureOpenAIEmbeddingsConfiguration = this._configuration.GetSection("AzureOpenAIEmbeddings").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIEmbeddingsConfiguration);

        IKernel target = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);

                config.AddAzureOpenAIEmbeddingGenerationService(
                    serviceId: azureOpenAIEmbeddingsConfiguration.ServiceId,
                    deploymentName: azureOpenAIEmbeddingsConfiguration.DeploymentName,
                    endpoint: azureOpenAIEmbeddingsConfiguration.Endpoint,
                    apiKey: azureOpenAIEmbeddingsConfiguration.ApiKey);

                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
            })
            .WithMemoryStorage(new VolatileMemoryStore())
            .Build();

        // Import all sample skills available for demonstration purposes.
        TestHelpers.ImportSampleSkills(target);

        var emailSkill = target.ImportSkill(new EmailSkill());

        var plannerSKill = target.ImportSkill(new PlannerSkill(target));

        // Act
        ContextVariables variables = new(prompt);
        variables.Set(PlannerSkill.Parameters.ExcludedSkills, "IntentDetectionSkill,FunSkill");
        variables.Set(PlannerSkill.Parameters.ExcludedFunctions, "EmailTo");
        variables.Set(PlannerSkill.Parameters.IncludedFunctions, "Continue");
        variables.Set(PlannerSkill.Parameters.MaxRelevantFunctions, "9");
        variables.Set(PlannerSkill.Parameters.RelevancyThreshold, "0.77");
        SKContext actual = await target.RunAsync(variables, plannerSKill["CreatePlan"]).ConfigureAwait(true);

        // Assert
        Assert.Empty(actual.LastErrorDescription);
        Assert.False(actual.ErrorOccurred);
        Assert.Contains(expectedAnswerContains, actual.Result, StringComparison.InvariantCultureIgnoreCase);
    }

    [Theory]
    [InlineData("If is morning tell me a joke about coffee",
        "function._GLOBAL_FUNCTIONS_.Hour", 1,
        "function.FunSkill.Joke", 1,
        "<if condition=\"", 1,
        "</if>", 1,
        "<else>", 0,
        "</else>", 0)]
    [InlineData("If is morning tell me a joke about coffee otherwise tell me a joke about the sun ",
        "function._GLOBAL_FUNCTIONS_.Hour", 1,
        "function.FunSkill.Joke", 2,
        "<if condition=\"", 1,
        "</if>", 1,
        "<else>", 1,
        "</else>", 1)]
    [InlineData("If is morning tell me a joke about coffee otherwise tell me a joke about the sun but if its night I want a joke about the moon",
        "function._GLOBAL_FUNCTIONS_.Hour", 1,
        "function.FunSkill.Joke", 3,
        "<if condition=\"", 2,
        "</if>", 2,
        "<else>", 2,
        "</else>", 2)]
    public async Task CreatePlanShouldHaveIfElseConditionalStatementsAndBeAbleToExecuteAsync(string prompt, params object[] expectedAnswerContainsAtLeast)
    {
        // Arrange

        Dictionary<string, int> expectedAnswerContainsDictionary = new();
        for (int i = 0; i < expectedAnswerContainsAtLeast.Length; i += 2)
        {
            string? key = expectedAnswerContainsAtLeast[i].ToString();
            int value = Convert.ToInt32(expectedAnswerContainsAtLeast[i + 1], CultureInfo.InvariantCulture);
            expectedAnswerContainsDictionary.Add(key!, value);
        }

        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        IKernel target = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);

                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
            })
            .Build();

        TestHelpers.GetSkill("FunSkill", target);
        target.ImportSkill(new TimeSkill());
        var plannerSKill = target.ImportSkill(new PlannerSkill(target));

        // Act
        SKContext createdPlanContext = await target.RunAsync(prompt, plannerSKill["CreatePlan"]).ConfigureAwait(true);
        await target.RunAsync(createdPlanContext.Variables.Clone(), plannerSKill["ExecutePlan"]).ConfigureAwait(false);

        // Assert
        Assert.Empty(createdPlanContext.LastErrorDescription);
        Assert.False(createdPlanContext.ErrorOccurred);

        foreach ((string? matchingExpression, int minimumExpectedCount) in expectedAnswerContainsDictionary)
        {
            if (minimumExpectedCount > 0)
            {
                Assert.Contains(matchingExpression, createdPlanContext.Variables[SkillPlan.PlanKey], StringComparison.InvariantCultureIgnoreCase);
            }

            var numberOfMatches = Regex.Matches(createdPlanContext.Variables[SkillPlan.PlanKey], matchingExpression, RegexOptions.IgnoreCase).Count;
            Assert.True(numberOfMatches >= minimumExpectedCount,
                $"Minimal number of matches below expected. Current: {numberOfMatches} Expected: {minimumExpectedCount} - Match: {matchingExpression}");
        }
    }

    [Theory]
    [InlineData("Start with a X number equals to the current minutes of the clock and remove 20 from this number until it becomes 0. After that tell me a math style joke where the input is X number + \"bananas\"",
        "function.TimeSkill.Minute", 1,
        "function.FunSkill.Joke", 1,
        "<while condition=\"", 1,
        "</while>", 1)]
    [InlineData("Until time is not noon wait 5 seconds after that check again and if it is create a creative joke",
        "function.TimeSkill.Hour", 1,
        "function.FunSkill.Joke", 1,
        "function.WaitSkill.Seconds", 1,
        "<while condition=\"", 1,
        "</while>", 1)]
    [InlineData("I want a nested loop O(n²) using the current date",
        "function.TimeSkill.Hour", 1,
        "<while condition=\"", 2,
        "</while>", 2)]
    public async Task CreatePlanShouldHaveWhileConditionalStatementsAndBeAbleToExecuteAsync(string prompt, params object[] expectedAnswerContainsAtLeast)
    {
        // Arrange

        Dictionary<string, int> expectedAnswerContainsDictionary = new();
        for (int i = 0; i < expectedAnswerContainsAtLeast.Length; i += 2)
        {
            string? key = expectedAnswerContainsAtLeast[i].ToString();
            int value = Convert.ToInt32(expectedAnswerContainsAtLeast[i + 1], CultureInfo.InvariantCulture);
            expectedAnswerContainsDictionary.Add(key!, value);
        }

        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        IKernel target = Kernel.Builder
            .WithLogger(this._logger)
            .Configure(config =>
            {
                config.AddAzureOpenAITextCompletionService(
                    serviceId: azureOpenAIConfiguration.ServiceId,
                    deploymentName: azureOpenAIConfiguration.DeploymentName,
                    endpoint: azureOpenAIConfiguration.Endpoint,
                    apiKey: azureOpenAIConfiguration.ApiKey);

                config.SetDefaultTextCompletionService(azureOpenAIConfiguration.ServiceId);
            })
            .Build();

        TestHelpers.GetSkill("FunSkill", target);
        target.ImportSkill(new TimeSkill(), "TimeSkill");
        target.ImportSkill(new MathSkill(), "MathSkill");
        target.ImportSkill(new WaitSkill(), "WaitSkill");
        var plannerSKill = target.ImportSkill(new PlannerSkill(target));

        // Act
        SKContext createdPlanContext = await target.RunAsync(prompt, plannerSKill["CreatePlan"]).ConfigureAwait(true);
        await target.RunAsync(createdPlanContext.Variables.Clone(), plannerSKill["ExecutePlan"]).ConfigureAwait(false);

        // Assert
        Assert.Empty(createdPlanContext.LastErrorDescription);
        Assert.False(createdPlanContext.ErrorOccurred);

        foreach ((string? matchingExpression, int minimumExpectedCount) in expectedAnswerContainsDictionary)
        {
            if (minimumExpectedCount > 0)
            {
                Assert.Contains(matchingExpression, createdPlanContext.Variables[SkillPlan.PlanKey], StringComparison.InvariantCultureIgnoreCase);
            }

            var numberOfMatches = Regex.Matches(createdPlanContext.Variables[SkillPlan.PlanKey], matchingExpression, RegexOptions.IgnoreCase).Count;
            Assert.True(numberOfMatches >= minimumExpectedCount,
                $"Minimal number of matches below expected. Current: {numberOfMatches} Expected: {minimumExpectedCount} - Match: {matchingExpression}");
        }
    }

    private readonly XunitLogger<object> _logger;
    private readonly RedirectOutput _testOutputHelper;
    private readonly IConfigurationRoot _configuration;

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~PlannerSkillTests()
    {
        this.Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._logger.Dispose();
            this._testOutputHelper.Dispose();
        }
    }

    internal class EmailSkill
    {
        [SKFunction("Given an e-mail and message body, send an email")]
        [SKFunctionInput(Description = "The body of the email message to send.")]
        [SKFunctionContextParameter(Name = "email_address", Description = "The email address to send email to.")]
        public Task<SKContext> SendEmailAsync(string input, SKContext context)
        {
            context.Variables.Update($"Sent email to: {context.Variables["email_address"]}. Body: {input}");
            return Task.FromResult(context);
        }

        [SKFunction("Given a name, find email address")]
        [SKFunctionInput(Description = "The name of the person to email.")]
        public Task<SKContext> GetEmailAddressAsync(string input, SKContext context)
        {
            context.Log.LogDebug("Returning hard coded email for {0}", input);
            context.Variables.Update("johndoe1234@example.com");
            return Task.FromResult(context);
        }

        [SKFunction("Write a short poem for an e-mail")]
        [SKFunctionInput(Description = "The topic of the poem.")]
        public Task<SKContext> WritePoemAsync(string input, SKContext context)
        {
            context.Variables.Update($"Roses are red, violets are blue, {input} is hard, so is this test.");
            return Task.FromResult(context);
        }
    }
}
