﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Anthropic;
using Microsoft.SemanticKernel.Connectors.Anthropic.Core;
using Xunit;

namespace SemanticKernel.Connectors.Anthropic.UnitTests;

/// <summary>
/// Unit tests for <see cref="AnthropicToolCallBehavior"/>
/// </summary>
public sealed class AnthropicToolCallBehaviorTests
{
    [Fact]
    public void EnableKernelFunctionsReturnsCorrectKernelFunctionsInstance()
    {
        // Arrange & Act
        var behavior = AnthropicToolCallBehavior.EnableKernelFunctions;

        // Assert
        Assert.IsType<AnthropicToolCallBehavior.KernelFunctions>(behavior);
        Assert.Equal(0, behavior.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void AutoInvokeKernelFunctionsReturnsCorrectKernelFunctionsInstance()
    {
        // Arrange & Act
        var behavior = AnthropicToolCallBehavior.AutoInvokeKernelFunctions;

        // Assert
        Assert.IsType<AnthropicToolCallBehavior.KernelFunctions>(behavior);
        Assert.Equal(5, behavior.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void EnableFunctionsReturnsEnabledFunctionsInstance()
    {
        // Arrange & Act
        List<AnthropicFunction> functions =
            [new AnthropicFunction("Plugin", "Function", "description", [], null)];
        var behavior = AnthropicToolCallBehavior.EnableFunctions(functions);

        // Assert
        Assert.IsType<AnthropicToolCallBehavior.EnabledFunctions>(behavior);
    }

    [Fact]
    public void KernelFunctionsConfigureClaudeRequestWithNullKernelDoesNotAddTools()
    {
        // Arrange
        var kernelFunctions = new AnthropicToolCallBehavior.KernelFunctions(autoInvoke: false);
        var claudeRequest = new AnthropicRequest();

        // Act
        kernelFunctions.ConfigureClaudeRequest(null, claudeRequest);

        // Assert
        Assert.Null(claudeRequest.Tools);
    }

    [Fact]
    public void KernelFunctionsConfigureClaudeRequestWithoutFunctionsDoesNotAddTools()
    {
        // Arrange
        var kernelFunctions = new AnthropicToolCallBehavior.KernelFunctions(autoInvoke: false);
        var claudeRequest = new AnthropicRequest();
        var kernel = Kernel.CreateBuilder().Build();

        // Act
        kernelFunctions.ConfigureClaudeRequest(kernel, claudeRequest);

        // Assert
        Assert.Null(claudeRequest.Tools);
    }

    [Fact]
    public void KernelFunctionsConfigureClaudeRequestWithFunctionsAddsTools()
    {
        // Arrange
        var kernelFunctions = new AnthropicToolCallBehavior.KernelFunctions(autoInvoke: false);
        var claudeRequest = new AnthropicRequest();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = GetTestPlugin();
        kernel.Plugins.Add(plugin);

        // Act
        kernelFunctions.ConfigureClaudeRequest(kernel, claudeRequest);

        // Assert
        AssertFunctions(claudeRequest);
    }

    [Fact]
    public void EnabledFunctionsConfigureClaudeRequestWithoutFunctionsDoesNotAddTools()
    {
        // Arrange
        var enabledFunctions = new AnthropicToolCallBehavior.EnabledFunctions([], autoInvoke: false);
        var claudeRequest = new AnthropicRequest();

        // Act
        enabledFunctions.ConfigureClaudeRequest(null, claudeRequest);

        // Assert
        Assert.Null(claudeRequest.Tools);
    }

    [Fact]
    public void EnabledFunctionsConfigureClaudeRequestWithAutoInvokeAndNullKernelThrowsException()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => AnthropicKernelFunctionMetadataExtensions.ToClaudeFunction(function));
        var enabledFunctions = new AnthropicToolCallBehavior.EnabledFunctions(functions, autoInvoke: true);
        var claudeRequest = new AnthropicRequest();

        // Act & Assert
        var exception = Assert.Throws<KernelException>(() => enabledFunctions.ConfigureClaudeRequest(null, claudeRequest));
        Assert.Equal(
            $"Auto-invocation with {nameof(AnthropicToolCallBehavior.EnabledFunctions)} is not supported when no kernel is provided.",
            exception.Message);
    }

    [Fact]
    public void EnabledFunctionsConfigureClaudeRequestWithAutoInvokeAndEmptyKernelThrowsException()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => function.ToClaudeFunction());
        var enabledFunctions = new AnthropicToolCallBehavior.EnabledFunctions(functions, autoInvoke: true);
        var claudeRequest = new AnthropicRequest();
        var kernel = Kernel.CreateBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<KernelException>(() => enabledFunctions.ConfigureClaudeRequest(kernel, claudeRequest));
        Assert.Equal(
            $"The specified {nameof(AnthropicToolCallBehavior.EnabledFunctions)} function MyPlugin{AnthropicFunction.NameSeparator}MyFunction is not available in the kernel.",
            exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnabledFunctionsConfigureClaudeRequestWithKernelAndPluginsAddsTools(bool autoInvoke)
    {
        // Arrange
        var plugin = GetTestPlugin();
        var functions = plugin.GetFunctionsMetadata().Select(function => function.ToClaudeFunction());
        var enabledFunctions = new AnthropicToolCallBehavior.EnabledFunctions(functions, autoInvoke);
        var claudeRequest = new AnthropicRequest();
        var kernel = Kernel.CreateBuilder().Build();

        kernel.Plugins.Add(plugin);

        // Act
        enabledFunctions.ConfigureClaudeRequest(kernel, claudeRequest);

        // Assert
        AssertFunctions(claudeRequest);
    }

    [Fact]
    public void EnabledFunctionsCloneReturnsCorrectClone()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => function.ToClaudeFunction());
        var toolcallbehavior = new AnthropicToolCallBehavior.EnabledFunctions(functions, autoInvoke: true);

        // Act
        var clone = toolcallbehavior.Clone();

        // Assert
        Assert.IsType<AnthropicToolCallBehavior.EnabledFunctions>(clone);
        Assert.NotSame(toolcallbehavior, clone);
        Assert.Equivalent(toolcallbehavior, clone, strict: true);
    }

    [Fact]
    public void KernelFunctionsCloneReturnsCorrectClone()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => function.ToClaudeFunction());
        var toolcallbehavior = new AnthropicToolCallBehavior.KernelFunctions(autoInvoke: true);

        // Act
        var clone = toolcallbehavior.Clone();

        // Assert
        Assert.IsType<AnthropicToolCallBehavior.KernelFunctions>(clone);
        Assert.NotSame(toolcallbehavior, clone);
        Assert.Equivalent(toolcallbehavior, clone, strict: true);
    }

    private static KernelPlugin GetTestPlugin()
    {
        var function = KernelFunctionFactory.CreateFromMethod(
            (string parameter1, string parameter2) => "Result1",
            "MyFunction",
            "Test Function",
            [new KernelParameterMetadata("parameter1"), new KernelParameterMetadata("parameter2")],
            new KernelReturnParameterMetadata { ParameterType = typeof(string), Description = "Function Result" });

        return KernelPluginFactory.CreateFromFunctions("MyPlugin", [function]);
    }

    private static void AssertFunctions(AnthropicRequest request)
    {
        Assert.NotNull(request.Tools);
        Assert.Single(request.Tools);

        var function = request.Tools[0];

        Assert.NotNull(function);

        Assert.Equal($"MyPlugin{AnthropicFunction.NameSeparator}MyFunction", function.Name);
        Assert.Equal("Test Function", function.Description);
        Assert.Equal("""{"type":"object","required":[],"properties":{"parameter1":{"type":"string"},"parameter2":{"type":"string"}}}""",
            JsonSerializer.Serialize(function.Parameters));
    }
}
