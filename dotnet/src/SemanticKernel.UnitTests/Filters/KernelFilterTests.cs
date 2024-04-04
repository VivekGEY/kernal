﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.Filters;

public class KernelFilterTests
{
    [Fact]
    public async Task FunctionFilterIsTriggeredAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var preFunctionInvocations = 0;
        var postFunctionInvocations = 0;

        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            preFunctionInvocations++;
            await next(context);
            postFunctionInvocations++;
        });

        // Act
        await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(1, preFunctionInvocations);
        Assert.Equal(1, postFunctionInvocations);
    }

    [Fact]
    public async Task FunctionFilterContextHasResultAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => "Result");

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            Assert.Null(context.Result);

            await next(context);

            Assert.NotNull(context.Result);
            Assert.Equal("Result", context.Result.ToString());
        });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("Result", result.ToString());
    }

    [Fact]
    public async Task PreInvocationFunctionFilterChangesArgumentAsync()
    {
        // Arrange
        const string OriginalInput = "OriginalInput";
        const string NewInput = "NewInput";

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            context.Arguments["originalInput"] = NewInput;
            await next(context);
        });

        var function = KernelFunctionFactory.CreateFromMethod((string originalInput) => originalInput);

        // Act
        var result = await kernel.InvokeAsync(function, new() { ["originalInput"] = OriginalInput });

        // Assert
        Assert.Equal(NewInput, result.GetValue<string>());
    }

    [Fact]
    public async Task FunctionFilterCancellationWorksCorrectlyAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var filterInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        // Act
        await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, filterInvocations);
        Assert.Equal(0, functionInvocations);
    }

    [Fact]
    public async Task FunctionFilterCancellationWorksCorrectlyOnStreamingAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var filterInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        // Act
        await foreach (var chunk in kernel.InvokeStreamingAsync(function))
        {
            functionInvocations++;
        }

        // Assert
        Assert.Equal(1, filterInvocations);
        Assert.Equal(0, functionInvocations);
    }

    [Fact]
    public async Task PostInvocationFunctionFilterReturnsModifiedResultAsync()
    {
        // Arrange
        const int OriginalResult = 42;
        const int NewResult = 84;

        var function = KernelFunctionFactory.CreateFromMethod(() => OriginalResult);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            await next(context);
            context.SetResultValue(NewResult);
        });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(NewResult, result.GetValue<int>());
    }

    [Fact]
    public async Task FunctionFiltersWithPromptsWorkCorrectlyAsync()
    {
        // Arrange
        var preFunctionInvocations = 0;
        var postFunctionInvocations = 0;
        var mockTextGeneration = this.GetMockTextGeneration();

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onFunctionInvocation: async (context, next) =>
            {
                preFunctionInvocations++;
                await next(context);
                postFunctionInvocations++;
            });

        var function = KernelFunctionFactory.CreateFromPrompt("Write a simple phrase about UnitTests");

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, preFunctionInvocations);
        Assert.Equal(1, postFunctionInvocations);
        mockTextGeneration.Verify(m => m.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }

    [Fact]
    public async Task PromptFiltersAreNotTriggeredForMethodsAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var preFilterInvocations = 0;
        var postFilterInvocations = 0;

        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(
            onPromptRendering: (context) =>
            {
                preFilterInvocations++;
            },
            onPromptRendered: (context) =>
            {
                postFilterInvocations++;
            });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(0, preFilterInvocations);
        Assert.Equal(0, postFilterInvocations);
    }

    [Fact]
    public async Task PromptFiltersAreTriggeredForPromptsAsync()
    {
        // Arrange
        var preFilterInvocations = 0;
        var postFilterInvocations = 0;
        var mockTextGeneration = this.GetMockTextGeneration();

        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendering: (context) =>
            {
                preFilterInvocations++;
            },
            onPromptRendered: (context) =>
            {
                postFilterInvocations++;
            });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, preFilterInvocations);
        Assert.Equal(1, postFilterInvocations);
    }

    [Fact]
    public async Task PromptFiltersAreTriggeredForPromptsStreamingAsync()
    {
        // Arrange
        var preFilterInvocations = 0;
        var postFilterInvocations = 0;
        var mockTextGeneration = this.GetMockTextGeneration();

        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendering: (context) =>
            {
                preFilterInvocations++;
            },
            onPromptRendered: (context) =>
            {
                postFilterInvocations++;
            });

        // Act
        await foreach (var chunk in kernel.InvokeStreamingAsync(function))
        {
        }

        // Assert
        Assert.Equal(1, preFilterInvocations);
        Assert.Equal(1, postFilterInvocations);
    }

    [Fact]
    public async Task PostInvocationPromptFilterChangesRenderedPromptAsync()
    {
        // Arrange
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");
        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendered: (context) =>
            {
                context.RenderedPrompt += " - updated from filter";
            });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        mockTextGeneration.Verify(m => m.GetTextContentsAsync("Prompt - updated from filter", It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task PostInvocationPromptFilterCancellationWorksCorrectlyAsync()
    {
        // Arrange
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");
        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendered: (context) =>
            {
                context.Cancel = true;
            });

        // Act
        var exception = await Assert.ThrowsAsync<KernelFunctionCanceledException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.Same(function, exception.Function);
        Assert.Same(kernel, exception.Kernel);
        Assert.Null(exception.FunctionResult?.GetValue<object>());
    }

    [Fact]
    public async Task FunctionAndPromptFiltersAreExecutedInCorrectOrderAsync()
    {
        // Arrange
        var builder = Kernel.CreateBuilder();
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");

        var executionOrder = new List<string>();

        var functionFilter1 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter1-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter1-Invoked");
        });

        var functionFilter2 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter2-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter2-Invoked");
        });

        var promptFilter1 = new FakePromptFilter(
            (context) => executionOrder.Add("PromptFilter1-Rendering"),
            (context) => executionOrder.Add("PromptFilter1-Rendered"));

        var promptFilter2 = new FakePromptFilter(
            (context) => executionOrder.Add("PromptFilter2-Rendering"),
            (context) => executionOrder.Add("PromptFilter2-Rendered"));

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);

        builder.Services.AddSingleton<IPromptFilter>(promptFilter1);
        builder.Services.AddSingleton<IPromptFilter>(promptFilter2);

        builder.Services.AddSingleton<ITextGenerationService>(mockTextGeneration.Object);

        var kernel = builder.Build();

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("FunctionFilter1-Invoking", executionOrder[0]);
        Assert.Equal("FunctionFilter2-Invoking", executionOrder[1]);
        Assert.Equal("PromptFilter1-Rendering", executionOrder[2]);
        Assert.Equal("PromptFilter2-Rendering", executionOrder[3]);
        Assert.Equal("PromptFilter1-Rendered", executionOrder[4]);
        Assert.Equal("PromptFilter2-Rendered", executionOrder[5]);
        Assert.Equal("FunctionFilter2-Invoked", executionOrder[6]);
        Assert.Equal("FunctionFilter1-Invoked", executionOrder[7]);
    }

    [Fact]
    public async Task MultipleFunctionFiltersCancellationWorksCorrectlyAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var filterInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var functionFilter1 = new FakeFunctionFilter(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        var functionFilter2 = new FakeFunctionFilter(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);

        var kernel = builder.Build();

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(0, functionInvocations);
        Assert.Equal(1, filterInvocations);
    }

    [Fact]
    public async Task DifferentWaysOfAddingFunctionFiltersWorkCorrectlyAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => "Result");
        var executionOrder = new List<string>();

        var functionFilter1 = new FakeFunctionFilter(async (context, next) =>
        {
            executionOrder.Add("FunctionFilter1-Invoking");
            await next(context);
        });

        var functionFilter2 = new FakeFunctionFilter(async (context, next) =>
        {
            executionOrder.Add("FunctionFilter2-Invoking");
            await next(context);
        });

        var builder = Kernel.CreateBuilder();

        // Act

        // Case #1 - Add filter to services
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);

        var kernel = builder.Build();

        // Case #2 - Add filter to kernel
        kernel.FunctionFilters.Add(functionFilter2);

        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("FunctionFilter1-Invoking", executionOrder[0]);
        Assert.Equal("FunctionFilter2-Invoking", executionOrder[1]);
    }

    [Fact]
    public async Task DifferentWaysOfAddingPromptFiltersWorkCorrectlyAsync()
    {
        // Arrange
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");
        var executionOrder = new List<string>();

        var promptFilter1 = new FakePromptFilter((context) => executionOrder.Add("PromptFilter1-Rendering"));
        var promptFilter2 = new FakePromptFilter((context) => executionOrder.Add("PromptFilter2-Rendering"));

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<ITextGenerationService>(mockTextGeneration.Object);

        // Act
        // Case #1 - Add filter to services
        builder.Services.AddSingleton<IPromptFilter>(promptFilter1);

        var kernel = builder.Build();

        // Case #2 - Add filter to kernel
        kernel.PromptFilters.Add(promptFilter2);

        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("PromptFilter1-Rendering", executionOrder[0]);
        Assert.Equal("PromptFilter2-Rendering", executionOrder[1]);
    }

    [Fact]
    public async Task InsertFilterInMiddleOfPipelineTriggersFiltersInCorrectOrderAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => "Result");
        var executionOrder = new List<string>();

        var functionFilter1 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter1-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter1-Invoked");
        });

        var functionFilter2 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter2-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter2-Invoked");
        });

        var functionFilter3 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter3-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter3-Invoked");
        });

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);

        var kernel = builder.Build();

        kernel.FunctionFilters.Insert(1, functionFilter3);

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("FunctionFilter1-Invoking", executionOrder[0]);
        Assert.Equal("FunctionFilter3-Invoking", executionOrder[1]);
        Assert.Equal("FunctionFilter2-Invoking", executionOrder[2]);
        Assert.Equal("FunctionFilter2-Invoked", executionOrder[3]);
        Assert.Equal("FunctionFilter3-Invoked", executionOrder[4]);
        Assert.Equal("FunctionFilter1-Invoked", executionOrder[5]);
    }

    [Fact]
    public async Task FunctionFilterReceivesInvocationExceptionAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => { throw new NotImplementedException(); });

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                // Exception will occur here.
                // Because it's not handled, it will be propagated to the caller.
                await next(context);
            });

        // Act
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task FunctionFilterCanCancelExceptionAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => { throw new NotImplementedException(); });

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (NotImplementedException)
                {
                    context.SetResultValue("Result ignoring exception.");
                }
            });

        // Act
        var result = await kernel.InvokeAsync(function);
        var resultValue = result.GetValue<string>();

        // Assert
        Assert.Equal("Result ignoring exception.", resultValue);
    }

    [Fact]
    public async Task FunctionFilterCanRethrowAnotherTypeOfExceptionAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => { throw new NotImplementedException(); });

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (NotImplementedException)
                {
                    throw new InvalidOperationException("Exception from filter");
                }
            });

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.NotNull(exception);
        Assert.Equal("Exception from filter", exception.Message);
    }

    [Fact]
    public async Task MultipleFunctionFiltersReceiveInvocationExceptionAsync()
    {
        // Arrange
        int filterInvocations = 0;
        KernelFunction function = KernelFunctionFactory.CreateFromMethod(() => { throw new NotImplementedException(); });

        async Task OnFunctionInvocationAsync(FunctionInvocationContext context, FunctionInvocationCallback next)
        {
            filterInvocations++;

            try
            {
                await next(context);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Assert.IsType<NotImplementedException>(exception);
                throw;
            }
        }

        var functionFilter1 = new FakeFunctionFilter(OnFunctionInvocationAsync);
        var functionFilter2 = new FakeFunctionFilter(OnFunctionInvocationAsync);
        var functionFilter3 = new FakeFunctionFilter(OnFunctionInvocationAsync);

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter3);

        var kernel = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(3, filterInvocations);
    }

    [Fact]
    public async Task MultipleFunctionFiltersPropagateExceptionCorrectlyAsync()
    {
        // Arrange
        KernelFunction function = KernelFunctionFactory.CreateFromMethod(() => { throw new KernelException("Exception from method"); });

        var functionFilter1 = new FakeFunctionFilter(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (KernelException exception)
            {
                Assert.Equal("Exception from functionFilter2", exception.Message);
                context.SetResultValue("Result from functionFilter1");
            }
        });

        var functionFilter2 = new FakeFunctionFilter(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (KernelException exception)
            {
                Assert.Equal("Exception from functionFilter3", exception.Message);
                throw new KernelException("Exception from functionFilter2");
            }
        });

        var functionFilter3 = new FakeFunctionFilter(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (KernelException exception)
            {
                Assert.Equal("Exception from method", exception.Message);
                throw new KernelException("Exception from functionFilter3");
            }
        });

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter3);

        var kernel = builder.Build();

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("Result from functionFilter1", result.ToString());
    }

    private Kernel GetKernelWithFilters(
        Func<FunctionInvocationContext, FunctionInvocationCallback, Task>? onFunctionInvocation = null,
        Action<PromptRenderingContext>? onPromptRendering = null,
        Action<PromptRenderedContext>? onPromptRendered = null,
        ITextGenerationService? textGenerationService = null)
    {
        var builder = Kernel.CreateBuilder();
        var promptFilter = new FakePromptFilter(onPromptRendering, onPromptRendered);

        // Add function filter before kernel construction
        if (onFunctionInvocation is not null)
        {
            var functionFilter = new FakeFunctionFilter(onFunctionInvocation);
            builder.Services.AddSingleton<IFunctionFilter>(functionFilter);
        }

        if (textGenerationService is not null)
        {
            builder.Services.AddSingleton<ITextGenerationService>(textGenerationService);
        }

        var kernel = builder.Build();

        // Add prompt filter after kernel construction
        kernel.PromptFilters.Add(promptFilter);

        return kernel;
    }

    private Mock<ITextGenerationService> GetMockTextGeneration()
    {
        var mockTextGeneration = new Mock<ITextGenerationService>();
        mockTextGeneration
            .Setup(m => m.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextContent> { new("result text") });

        mockTextGeneration
            .Setup(s => s.GetStreamingTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .Returns(new List<StreamingTextContent>() { new("result chunk") }.ToAsyncEnumerable());

        return mockTextGeneration;
    }

    private sealed class FakeFunctionFilter(
        Func<FunctionInvocationContext, FunctionInvocationCallback, Task>? onFunctionInvocation) : IFunctionFilter
    {
        private readonly Func<FunctionInvocationContext, FunctionInvocationCallback, Task>? _onFunctionInvocation = onFunctionInvocation;

        public Task OnFunctionInvocationAsync(FunctionInvocationContext context, FunctionInvocationCallback next) =>
            this._onFunctionInvocation?.Invoke(context, next) ?? Task.CompletedTask;
    }

    private sealed class FakePromptFilter(
        Action<PromptRenderingContext>? onPromptRendering = null,
        Action<PromptRenderedContext>? onPromptRendered = null) : IPromptFilter
    {
        private readonly Action<PromptRenderingContext>? _onPromptRendering = onPromptRendering;
        private readonly Action<PromptRenderedContext>? _onPromptRendered = onPromptRendered;

        public void OnPromptRendered(PromptRenderedContext context) =>
            this._onPromptRendered?.Invoke(context);

        public void OnPromptRendering(PromptRenderingContext context) =>
            this._onPromptRendering?.Invoke(context);
    }
}
