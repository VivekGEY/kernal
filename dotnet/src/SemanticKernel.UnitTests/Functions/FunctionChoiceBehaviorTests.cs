﻿// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using Microsoft.SemanticKernel;
using Xunit;

namespace SemanticKernel.UnitTests.Functions;

/// <summary>
/// Unit tests for <see cref="FunctionChoiceBehavior"/>
/// </summary>
public sealed class FunctionChoiceBehaviorTests
{
    private readonly Kernel _kernel;

    public FunctionChoiceBehaviorTests()
    {
        this._kernel = new Kernel();
    }

    [Fact]
    public void AutoFunctionChoiceShouldBeUsed()
    {
        // Act
        var choiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice();

        // Assert
        Assert.IsType<AutoFunctionChoiceBehavior>(choiceBehavior);
    }

    [Fact]
    public void RequiredFunctionChoiceShouldBeUsed()
    {
        // Act
        var choiceBehavior = FunctionChoiceBehavior.RequiredFunctionChoice();

        // Assert
        Assert.IsType<RequiredFunctionChoiceBehavior>(choiceBehavior);
    }

    [Fact]
    public void NoneFunctionChoiceShouldBeUsed()
    {
        // Act
        var choiceBehavior = FunctionChoiceBehavior.None;

        // Assert
        Assert.IsType<NoneFunctionChoiceBehavior>(choiceBehavior);
    }

    [Fact]
    public void AutoFunctionChoiceShouldAdvertiseKernelFunctionsAsAvailableOnes()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(functions: null);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);

        Assert.Null(config.RequiredFunctions);

        Assert.NotNull(config.AvailableFunctions);
        Assert.Equal(3, config.AvailableFunctions.Count());
        Assert.Contains(config.AvailableFunctions, f => f.Name == "Function1");
        Assert.Contains(config.AvailableFunctions, f => f.Name == "Function2");
        Assert.Contains(config.AvailableFunctions, f => f.Name == "Function3");
    }

    [Fact]
    public void AutoFunctionChoiceShouldAdvertiseProvidedFunctionsAsAvailableOnes()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(functions: [plugin.ElementAt(0), plugin.ElementAt(1)]);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);

        Assert.Null(config.RequiredFunctions);

        Assert.NotNull(config.AvailableFunctions);
        Assert.Equal(2, config.AvailableFunctions.Count());
        Assert.Contains(config.AvailableFunctions, f => f.Name == "Function1");
        Assert.Contains(config.AvailableFunctions, f => f.Name == "Function2");
    }

    [Fact]
    public void AutoFunctionChoiceShouldAllowAutoInvocation()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(autoInvoke: true);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);
        Assert.Equal(5, config.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void AutoFunctionChoiceShouldAllowManualInvocation()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.AutoFunctionChoice(autoInvoke: false);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0, config.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void RequiredFunctionChoiceShouldAdvertiseKernelFunctionsAsRequiredOnes()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.RequiredFunctionChoice(functions: null);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);

        Assert.Null(config.AvailableFunctions);

        Assert.NotNull(config.RequiredFunctions);
        Assert.Equal(3, config.RequiredFunctions.Count());
        Assert.Contains(config.RequiredFunctions, f => f.Name == "Function1");
        Assert.Contains(config.RequiredFunctions, f => f.Name == "Function2");
        Assert.Contains(config.RequiredFunctions, f => f.Name == "Function3");
    }

    [Fact]
    public void RequiredFunctionChoiceShouldAdvertiseProvidedFunctionsAsRequiredOnes()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.RequiredFunctionChoice(functions: [plugin.ElementAt(0), plugin.ElementAt(1)]);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);

        Assert.Null(config.AvailableFunctions);

        Assert.NotNull(config.RequiredFunctions);
        Assert.Equal(2, config.RequiredFunctions.Count());
        Assert.Contains(config.RequiredFunctions, f => f.Name == "Function1");
        Assert.Contains(config.RequiredFunctions, f => f.Name == "Function2");
    }

    [Fact]
    public void RequiredFunctionChoiceShouldAllowAutoInvocation()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.RequiredFunctionChoice(autoInvoke: true);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);
        Assert.Equal(5, config.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void RequiredFunctionChoiceShouldAllowManualInvocation()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.RequiredFunctionChoice(autoInvoke: false);

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);
        Assert.Equal(0, config.MaximumAutoInvokeAttempts);
    }

    [Fact]
    public void NoneFunctionChoiceShouldAdvertiseNoFunctions()
    {
        // Arrange
        var plugin = GetTestPlugin();
        this._kernel.Plugins.Add(plugin);

        // Act
        var choiceBehavior = FunctionChoiceBehavior.None;

        var config = choiceBehavior.GetConfiguration(new() { Kernel = this._kernel });

        // Assert
        Assert.NotNull(config);

        Assert.Null(config.AvailableFunctions);
        Assert.Null(config.RequiredFunctions);
    }

    private static KernelPlugin GetTestPlugin()
    {
        var function1 = KernelFunctionFactory.CreateFromMethod(() => { }, "Function1");
        var function2 = KernelFunctionFactory.CreateFromMethod(() => { }, "Function2");
        var function3 = KernelFunctionFactory.CreateFromMethod(() => { }, "Function3");

        return KernelPluginFactory.CreateFromFunctions("MyPlugin", [function1, function2, function3]);
    }
}
