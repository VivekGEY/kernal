﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using NCalcPlugins;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// Example of telemetry in Semantic Kernel using Application Insights within console application.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// Log level to be used by <see cref="ILogger"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="LogLevel.Information"/> is set by default. <para />
    /// <see cref="LogLevel.Trace"/> will enable logging with more detailed information, including sensitive data. Should not be used in production. <para />
    /// </remarks>
    private const LogLevel MinLogLevel = LogLevel.Information;

    /// <summary>
    /// Instance of <see cref="ActivitySource"/> for the application activities.
    /// </summary>
    private static readonly ActivitySource s_activitySource = new("Telemetry.Example");

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task Main()
    {
        var connectionString = Env.Var("ApplicationInsights__ConnectionString");

        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Microsoft.SemanticKernel.*")
            .AddSource("Telemetry.Example")
            .AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("Microsoft.SemanticKernel.*")
            .AddAzureMonitorMetricExporter(options => options.ConnectionString = connectionString)
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            // Add OpenTelemetry as a logging provider
            builder.AddOpenTelemetry(options =>
            {
                options.AddAzureMonitorLogExporter(options => options.ConnectionString = connectionString);
                // Format log messages. This is default to false.
                options.IncludeFormattedMessage = true;
            });
            builder.SetMinimumLevel(MinLogLevel);
        });

        var kernel = GetKernel(loggerFactory);
        var planner = GetSequentialPlanner(kernel);

        using var activity = s_activitySource.StartActivity("Main");

        Console.WriteLine("Operation/Trace ID:");
        Console.WriteLine(Activity.Current?.TraceId);

        var plan = await planner.CreatePlanAsync("Write a poem about John Doe, then translate it into Italian.");

        Console.WriteLine("Original plan:");
        Console.WriteLine(plan.ToPlanString());

        var result = await kernel.RunAsync(plan);

        Console.WriteLine("Result:");
        Console.WriteLine(result.GetValue<string>());
    }

    private static Kernel GetKernel(ILoggerFactory loggerFactory)
    {
        var folder = RepoFiles.SamplePluginsPath();
        var bingConnector = new BingConnector(Env.Var("Bing__ApiKey"));
        var webSearchEnginePlugin = new WebSearchEnginePlugin(bingConnector);

        var kernel = new KernelBuilder()
            .WithLoggerFactory(loggerFactory)
            .WithAzureOpenAIChatCompletionService(
                Env.Var("AzureOpenAI__ChatDeploymentName"),
                Env.Var("AzureOpenAI__Endpoint"),
                Env.Var("AzureOpenAI__ApiKey"))
            .Build();

        kernel.ImportPluginFromPromptDirectory(Path.Combine(folder, "SummarizePlugin"));
        kernel.ImportPluginFromPromptDirectory(Path.Combine(folder, "WriterPlugin"));

        kernel.ImportPluginFromObject(webSearchEnginePlugin, "WebSearch");
        kernel.ImportPluginFromObject<LanguageCalculatorPlugin>("advancedCalculator");
        kernel.ImportPluginFromObject<TimePlugin>();

        return kernel;
    }

    private static SequentialPlanner GetSequentialPlanner(
        Kernel kernel,
        int maxTokens = 1024)
    {
        var plannerConfig = new SequentialPlannerConfig { MaxTokens = maxTokens };

        return new SequentialPlanner(kernel, plannerConfig);
    }

    private static ActionPlanner GetActionPlanner(Kernel kernel)
    {
        return new ActionPlanner(kernel);
    }

    private static StepwisePlanner GetStepwisePlanner(
        Kernel kernel,
        int minIterationTimeMs = 1500,
        int maxTokens = 2000)
    {
        var plannerConfig = new StepwisePlannerConfig
        {
            MinIterationTimeMs = minIterationTimeMs,
            MaxTokens = maxTokens
        };

        return new StepwisePlanner(kernel, plannerConfig);
    }
}
