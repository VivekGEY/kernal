﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.GoogleVertexAI;
using Microsoft.SemanticKernel.Connectors.GoogleVertexAI.Core;
using Xunit;

namespace SemanticKernel.Connectors.GoogleVertexAI.UnitTests;

/// <summary>
/// Unit tests for <see cref="ToolCallBehavior"/>
/// </summary>
public sealed class ToolCallBehaviorTests
{
    [Fact]
    public void EnableKernelFunctionsReturnsCorrectKernelFunctionsInstance()
    {
        // Arrange & Act
        var behavior = ToolCallBehavior.EnableKernelFunctions;

        // Assert
        Assert.IsType<ToolCallBehavior.KernelFunctions>(behavior);
        Assert.Equal(0, behavior.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void AutoInvokeKernelFunctionsReturnsCorrectKernelFunctionsInstance()
    {
        // Arrange & Act
        var behavior = ToolCallBehavior.AutoInvokeKernelFunctions;

        // Assert
        Assert.IsType<ToolCallBehavior.KernelFunctions>(behavior);
        Assert.Equal(5, behavior.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void EnableFunctionsReturnsEnabledFunctionsInstance()
    {
        // Arrange & Act
        List<GeminiFunction> functions =
            [new GeminiFunction("Plugin", "Function", "description", [], null)];
        var behavior = ToolCallBehavior.EnableFunctions(functions);

        // Assert
        Assert.IsType<ToolCallBehavior.EnabledFunctions>(behavior);
    }

    [Fact]
    public void KernelFunctionsConfigureGeminiRequestWithNullKernelDoesNotAddTools()
    {
        // Arrange
        var kernelFunctions = new ToolCallBehavior.KernelFunctions(autoInvoke: false);
        var geminiRequest = new GeminiRequest();

        // Act
        kernelFunctions.ConfigureGeminiRequest(null, geminiRequest);

        // Assert
        Assert.Null(geminiRequest.Tools);
    }

    [Fact]
    public void KernelFunctionsConfigureGeminiRequestWithoutFunctionsDoesNotAddTools()
    {
        // Arrange
        var kernelFunctions = new ToolCallBehavior.KernelFunctions(autoInvoke: false);
        var geminiRequest = new GeminiRequest();
        var kernel = Kernel.CreateBuilder().Build();

        // Act
        kernelFunctions.ConfigureGeminiRequest(kernel, geminiRequest);

        // Assert
        Assert.Null(geminiRequest.Tools);
    }

    [Fact]
    public void KernelFunctionsConfigureGeminiRequestWithFunctionsAddsTools()
    {
        // Arrange
        var kernelFunctions = new ToolCallBehavior.KernelFunctions(autoInvoke: false);
        var geminiRequest = new GeminiRequest();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = GetTestPlugin();
        kernel.Plugins.Add(plugin);

        // Act
        kernelFunctions.ConfigureGeminiRequest(kernel, geminiRequest);

        // Assert
        AssertFunctions(geminiRequest);
    }

    [Fact]
    public void EnabledFunctionsConfigureGeminiRequestWithoutFunctionsDoesNotAddTools()
    {
        // Arrange
        var enabledFunctions = new ToolCallBehavior.EnabledFunctions([], autoInvoke: false);
        var geminiRequest = new GeminiRequest();

        // Act
        enabledFunctions.ConfigureGeminiRequest(null, geminiRequest);

        // Assert
        Assert.Null(geminiRequest.Tools);
    }

    [Fact]
    public void EnabledFunctionsConfigureGeminiRequestWithAutoInvokeAndNullKernelThrowsException()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => GeminiKernelFunctionMetadataExtensions.ToGeminiFunction(function));
        var enabledFunctions = new ToolCallBehavior.EnabledFunctions(functions, autoInvoke: true);
        var geminiRequest = new GeminiRequest();

        // Act & Assert
        var exception = Assert.Throws<KernelException>(() => enabledFunctions.ConfigureGeminiRequest(null, geminiRequest));
        Assert.Equal(
            $"Auto-invocation with {nameof(ToolCallBehavior.EnabledFunctions)} is not supported when no kernel is provided.",
            exception.Message);
    }

    [Fact]
    public void EnabledFunctionsConfigureGeminiRequestWithAutoInvokeAndEmptyKernelThrowsException()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => function.ToGeminiFunction());
        var enabledFunctions = new ToolCallBehavior.EnabledFunctions(functions, autoInvoke: true);
        var geminiRequest = new GeminiRequest();
        var kernel = Kernel.CreateBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<KernelException>(() => enabledFunctions.ConfigureGeminiRequest(kernel, geminiRequest));
        Assert.Equal(
            $"The specified {nameof(ToolCallBehavior.EnabledFunctions)} function MyPlugin{GeminiFunction.NameSeparator}MyFunction is not available in the kernel.",
            exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnabledFunctionsConfigureGeminiRequestWithKernelAndPluginsAddsTools(bool autoInvoke)
    {
        // Arrange
        var plugin = GetTestPlugin();
        var functions = plugin.GetFunctionsMetadata().Select(function => function.ToGeminiFunction());
        var enabledFunctions = new ToolCallBehavior.EnabledFunctions(functions, autoInvoke);
        var geminiRequest = new GeminiRequest();
        var kernel = Kernel.CreateBuilder().Build();

        kernel.Plugins.Add(plugin);

        // Act
        enabledFunctions.ConfigureGeminiRequest(kernel, geminiRequest);

        // Assert
        AssertFunctions(geminiRequest);
    }

    [Fact]
    public void EnabledFunctionsCloneReturnsCorrectClone()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => function.ToGeminiFunction());
        var toolcallbehavior = new ToolCallBehavior.EnabledFunctions(functions, autoInvoke: true);

        // Act
        var clone = toolcallbehavior.Clone();

        // Assert
        Assert.IsType<ToolCallBehavior.EnabledFunctions>(clone);
        Assert.NotSame(toolcallbehavior, clone);
        Assert.Equivalent(toolcallbehavior, clone, strict: true);
    }

    [Fact]
    public void KernelFunctionsCloneReturnsCorrectClone()
    {
        // Arrange
        var functions = GetTestPlugin().GetFunctionsMetadata().Select(function => function.ToGeminiFunction());
        var toolcallbehavior = new ToolCallBehavior.KernelFunctions(autoInvoke: true);

        // Act
        var clone = toolcallbehavior.Clone();

        // Assert
        Assert.IsType<ToolCallBehavior.KernelFunctions>(clone);
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

    private static void AssertFunctions(GeminiRequest request)
    {
        Assert.NotNull(request.Tools);
        Assert.Single(request.Tools);
        Assert.Single(request.Tools[0].Functions);

        var function = request.Tools[0].Functions[0];

        Assert.NotNull(function);

        Assert.Equal($"MyPlugin{GeminiFunction.NameSeparator}MyFunction", function.Name);
        Assert.Equal("Test Function", function.Description);
        Assert.Equal("""{"type":"object","required":[],"properties":{"parameter1":{"type":"string"},"parameter2":{"type":"string"}}}""",
            JsonSerializer.Serialize(function.Parameters));
    }
}
