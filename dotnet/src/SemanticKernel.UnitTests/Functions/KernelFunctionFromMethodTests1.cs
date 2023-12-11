﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.Functions;

public sealed class KernelFunctionFromMethodTests1
{
    private static readonly KernelFunction s_nopFunction = KernelFunctionFactory.CreateFromMethod(() => { });

    private readonly Kernel _kernel;
    private readonly Mock<ILoggerFactory> _logger;

    private static string s_expected = string.Empty;
    private static string s_actual = string.Empty;

    public KernelFunctionFromMethodTests1()
    {
        this._kernel = new Kernel();
        this._logger = new Mock<ILoggerFactory>();

        s_expected = Guid.NewGuid().ToString("D");
    }

    [Fact]
    public async Task ItSupportsStaticVoidVoidAsync()
    {
        // Arrange
        static void Test()
        {
            s_actual = s_expected;
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        await function.InvokeAsync(this._kernel);

        // Assert
        Assert.Equal(s_expected, s_actual);
    }

    [Fact]
    public async Task ItSupportsStaticVoidStringAsync()
    {
        // Arrange
        static string Test()
        {
            s_actual = s_expected;
            return s_expected;
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Equal(s_expected, result.GetValue<string>());
        Assert.Equal(s_expected, result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticVoidTaskStringAsync()
    {
        // Arrange
        static Task<string> Test()
        {
            s_actual = s_expected;
            return Task.FromResult(s_expected);
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Equal(s_expected, result.GetValue<string>());
        Assert.Equal(s_expected, result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticVoidValueTaskStringAsync()
    {
        // Arrange
        static async ValueTask<string> Test()
        {
            s_actual = s_expected;
            await Task.Delay(1);
            return s_expected;
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Equal(s_expected, result.GetValue<string>());
        Assert.Equal(s_expected, result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticVoidAsync()
    {
        // Arrange
        static void Test()
        {
            s_actual = s_expected;
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticAsync()
    {
        // Arrange
        static string Test(string someVar)
        {
            s_actual = someVar;
            return "abc";
        }

        var arguments = new KernelArguments();
        arguments["someVar"] = s_expected;

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Equal("abc", result.GetValue<string>());
        Assert.Equal("abc", result.ToString());
    }

    [Fact]
    public async Task ItSupportsInstanceStringStringNullableAsync()
    {
        // Arrange
        int invocationCount = 0;

        string? Test(string someVar)
        {
            invocationCount++;
            s_actual = someVar;
            return "abc";
        }

        var arguments = new KernelArguments();
        arguments["someVar"] = s_expected;

        // Act
        Func<string, string?> method = Test;
        var function = KernelFunctionFactory.CreateFromMethod(method, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(1, invocationCount);
        Assert.Equal(s_expected, s_actual);
        Assert.Equal("abc", result.GetValue<string>());
        Assert.Equal("abc", result.ToString());
    }

    [Fact]
    public async Task ItSupportsInstanceStringTaskAsync()
    {
        // Arrange
        int invocationCount = 0;

        async Task TestAsync(string canary)
        {
            await Task.Delay(0);
            invocationCount++;
            s_actual = canary;
        }

        var arguments = new KernelArguments();
        arguments["canary"] = s_expected;

        // Act
        Func<string, Task> method = TestAsync;
        var function = KernelFunctionFactory.CreateFromMethod(method, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(1, invocationCount);
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsInstanceStringVoidAsync()
    {
        // Arrange
        int invocationCount = 0;

        void Test(string input)
        {
            invocationCount++;
            s_actual = s_expected + input;
        }

        var arguments = new KernelArguments(".blah");

        // Act
        Action<string> method = Test;
        var function = KernelFunctionFactory.CreateFromMethod(method, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(1, invocationCount);
        Assert.Equal(s_expected + ".blah", s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsInstanceStringStringAsync()
    {
        // Arrange
        int invocationCount = 0;

        string Test(string input)
        {
            invocationCount++;
            return input;
        }

        var arguments = new KernelArguments("foo-bar");

        // Act
        Func<string, string> method = Test;
        var function = KernelFunctionFactory.CreateFromMethod(method, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(1, invocationCount);
        Assert.Equal("foo-bar", result.GetValue<string>());
        Assert.Equal("foo-bar", result.ToString());
    }

    [Fact]
    public async Task ItSupportsInstanceStringTaskStringAsync()
    {
        // Arrange
        int invocationCount = 0;

        Task<string> Test(string input)
        {
            invocationCount++;
            return Task.FromResult("hello there");
        }

        var arguments = new KernelArguments(string.Empty);

        // Act
        Func<string, Task<string>> method = Test;
        var function = KernelFunctionFactory.CreateFromMethod(method, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(1, invocationCount);
        Assert.Equal("hello there", result.GetValue<string>());
    }

    [Fact]
    public async Task ItSupportsInstanceKernelVoidAsync()
    {
        // Arrange
        int invocationCount = 0;
        Kernel? actualKernel = null;

        void Test(Kernel kernel)
        {
            invocationCount++;
            actualKernel = kernel;
        }

        var arguments = new KernelArguments();

        // Act
        Action<Kernel> method = Test;
        var function = KernelFunctionFactory.CreateFromMethod(method);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(1, invocationCount);
        Assert.Equal(this._kernel, actualKernel);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticStringStringAsync()
    {
        // Arrange
        static string Test(string input)
        {
            s_actual = input;
            return "new data";
        }

        var arguments = new KernelArguments(s_expected);

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Equal("new data", result.GetValue<string>());
        Assert.Equal("new data", result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticStringTaskStringAsync()
    {
        // Arrange
        static Task<string> Test(string input)
        {
            s_actual = input;
            return Task.FromResult("new data");
        }

        var arguments = new KernelArguments(s_expected);

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Equal("new data", result.GetValue<string>());
        Assert.Equal("new data", result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticValueTaskAsync()
    {
        // Arrange
        s_expected = "testabc";

        static ValueTask Test(string input)
        {
            s_actual = input + "abc";
            return new ValueTask();
        }

        var arguments = new KernelArguments("test");

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticStringTaskAsync()
    {
        // Arrange
        static Task TestAsync(string input)
        {
            s_actual = s_expected;
            return Task.CompletedTask;
        }

        var arguments = new KernelArguments(string.Empty);

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(TestAsync, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticStringValueTaskAsync()
    {
        // Arrange
        static ValueTask TestAsync(string input)
        {
            s_actual = s_expected;
            return default;
        }

        var arguments = new KernelArguments(string.Empty);

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(TestAsync, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticTaskAsync()
    {
        // Arrange
        s_expected = "x y z";

        static Task TestAsync()
        {
            s_actual = s_expected;
            return Task.CompletedTask;
        }

        var arguments = new KernelArguments();

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(TestAsync, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticStringAsync()
    {
        // Arrange
        s_expected = "x y z";

        static Task TestAsync(string input)
        {
            s_actual = input;
            return Task.CompletedTask;
        }

        var arguments = new KernelArguments("x y z");

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(TestAsync, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsStaticVoidTaskAsync()
    {
        // Arrange
        static Task TestAsync()
        {
            s_actual = s_expected;
            return Task.CompletedTask;
        }

        var arguments = new KernelArguments();

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(TestAsync, loggerFactory: this._logger.Object);
        Assert.NotNull(function);

        var result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(s_expected, s_actual);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItSupportsUsingNamedInputValueAsync()
    {
        static string Test(string input) => "Result: " + input;

        var arguments = new KernelArguments("input value");

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal("Result: input value", result.GetValue<string>());
        Assert.Equal("Result: input value", result.ToString());
    }

    [Fact]
    public async Task ItSupportsUsingNonNamedInputValueAsync()
    {
        static string Test(string other) => "Result: " + other;

        var arguments = new KernelArguments("input value");

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal("Result: input value", result.GetValue<string>());
        Assert.Equal("Result: input value", result.ToString());
    }

    [Fact]
    public async Task ItSupportsUsingNonNamedInputValueEvenWhenThereAreMultipleParametersAsync()
    {
        static string Test(int something, long orother) => "Result: " + (something + orother);

        var arguments = new KernelArguments("42") { ["orother"] = 8 };

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal("Result: 50", result.GetValue<string>());
        Assert.Equal("Result: 50", result.ToString());
    }

    [Fact]
    public async Task ItSupportsPreferringNamedValueOverInputAsync()
    {
        static string Test(string other) => "Result: " + other;

        var arguments = new KernelArguments("input value") { ["other"] = "other value" };

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal("Result: other value", result.GetValue<string>());
        Assert.Equal("Result: other value", result.ToString());
    }

    [Fact]
    public async Task ItSupportsOverridingNameWithAttributeAsync()
    {
        static string Test([Description("description")] string input) => "Result: " + input;

        var arguments = new KernelArguments("input value") { ["other"] = "other value" };

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal("Result: input value", result.GetValue<string>());
        Assert.Equal("Result: input value", result.ToString());
    }

    [Fact]
    public async Task ItSupportNullDefaultValuesOverInputAsync()
    {
        static string Test(string? input = null, string? other = null) => "Result: " + (other is null);

        var arguments = new KernelArguments("input value");

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal("Result: True", result.GetValue<string>());
        Assert.Equal("Result: True", result.ToString());
    }

    [Fact]
    public async Task ItSupportFunctionResultAsync()
    {
        FunctionResult Test() => new(s_nopFunction, "fake-result", CultureInfo.InvariantCulture);

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fake-result", result.GetValue<string>());
        Assert.Equal("fake-result", result.ToString());
    }

    [Fact]
    public async Task ItSupportFunctionResultTaskAsync()
    {
        // Arrange
        Task<FunctionResult> Test()
        {
            var functionResult = new FunctionResult(s_nopFunction, "fake-result", CultureInfo.InvariantCulture);
            return Task.FromResult(functionResult);
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fake-result", result.GetValue<string>());
        Assert.Equal("fake-result", result.ToString());
    }

    [Fact]
    public async Task ItSupportFunctionResultValueTaskAsync()
    {
        // Arrange
        ValueTask<FunctionResult> Test()
        {
            var functionResult = new FunctionResult(s_nopFunction, "fake-result", CultureInfo.InvariantCulture);
            return ValueTask.FromResult(functionResult);
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fake-result", result.GetValue<string>());
        Assert.Equal("fake-result", result.ToString());
    }

    [Fact]
    public async Task ItSupportsConvertingFromManyTypesAsync()
    {
        static string Test(int a, long b, decimal c, Guid d, DateTimeOffset e, DayOfWeek? f) =>
            $"{a} {b} {c} {d} {e:R} {f}";

        var arguments = new KernelArguments();
        arguments["a"] = "1";
        arguments["b"] = -2;
        arguments["c"] = "1234";
        arguments["d"] = Guid.Parse("7e08cc00-1d71-4558-81ed-69929499dea1");
        arguments["e"] = "Thu, 25 May 2023 20:17:30 GMT";
        arguments["f"] = DayOfWeek.Monday;

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal("1 -2 1234 7e08cc00-1d71-4558-81ed-69929499dea1 Thu, 25 May 2023 20:17:30 GMT Monday", result.GetValue<string>());
        Assert.Equal("1 -2 1234 7e08cc00-1d71-4558-81ed-69929499dea1 Thu, 25 May 2023 20:17:30 GMT Monday", result.ToString());
    }

    [Fact]
    public async Task ItSupportsConvertingFromTypeConverterAttributedTypesAsync()
    {
        static int Test(MyCustomType mct) => mct.Value * 2;

        var arguments = new KernelArguments();
        arguments["mct"] = "42";

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        // Assert
        Assert.Equal(84, result.GetValue<int>());
        Assert.Equal("84", result.ToString());
    }

    [Theory]
    [InlineData((int)0, DayOfWeek.Sunday)]
    [InlineData((uint)1, DayOfWeek.Monday)]
    [InlineData((long)2, DayOfWeek.Tuesday)]
    [InlineData((ulong)3, DayOfWeek.Wednesday)]
    [InlineData((short)4, DayOfWeek.Thursday)]
    [InlineData((ushort)5, DayOfWeek.Friday)]
    [InlineData((byte)6, DayOfWeek.Saturday)]
    [InlineData((sbyte)0, DayOfWeek.Sunday)]
    public async Task ItSupportsConvertingAllIntegerTypesToEnumAsync(object argument, DayOfWeek expected)
    {
        // Arrange
        object? actual = null;

        var function = KernelFunctionFactory.CreateFromMethod((DayOfWeek dow) => actual = dow);

        // Act
        var result = await function.InvokeAsync(this._kernel, new() { ["dow"] = argument });

        // Assert
        Assert.Equal(expected, actual);
    }

    [TypeConverter(typeof(MyCustomTypeConverter))]
    private sealed class MyCustomType
    {
        public int Value { get; set; }
    }

#pragma warning disable CA1812 // Instantiated by reflection
    private sealed class MyCustomTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string);
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
            new MyCustomType { Value = int.Parse((string)value, culture) };
    }
#pragma warning restore CA1812

    [Fact]
    public async Task ItSupportsConvertingArgumentsFromStringToManyTypesAsync()
    {
        // Arrange
        var arguments = new KernelArguments("1");

        async Task AssertResult(Delegate d, object? expected, string? expectedString)
        {
            var result = await KernelFunctionFactory.CreateFromMethod(d, functionName: "Test")!.InvokeAsync(this._kernel, arguments);

            Assert.Equal(expected, result.GetValue<object?>());
            Assert.Equal(expectedString, result.ToString());
        }

        // Act/Assert
        await AssertResult((sbyte input) => input * 2, 2, "2");
        await AssertResult((byte input) => input * 2, 2, "2");
        await AssertResult((short input) => input * 2, 2, "2");
        await AssertResult((ushort input) => input * 2, 2, "2");
        await AssertResult((int input) => input * 2, 2, "2");
        await AssertResult((uint input) => input * 2, (uint)2, "2");
        await AssertResult((long input) => input * 2, (long)2, "2");
        await AssertResult((ulong input) => input * 2, (ulong)2, "2");
        await AssertResult((float input) => input * 2, (float)2, "2");
        await AssertResult((double input) => input * 2, (double)2, "2");
        await AssertResult((int input) => Task.FromResult(input * 2), 2, "2");
        await AssertResult((long input) => Task.FromResult(input * 2), (long)2, "2");
        await AssertResult((int input) => new ValueTask<int>(input * 2), 2, "2");
        await AssertResult((long input) => new ValueTask<long>(input * 2), (long)2, "2");
        await AssertResult((long? input) => input!.Value * 2, (long?)2, "2");
        await AssertResult((TimeSpan input) => TimeSpan.FromTicks(input.Ticks * 2), TimeSpan.FromDays(2), "2.00:00:00");
        await AssertResult((TimeSpan? input) => (int?)null, null, "");

        arguments[KernelArguments.InputParameterName] = "http://example.com/semantic";
        await AssertResult((Uri input) => new Uri(input, "kernel"), new Uri("http://example.com/kernel"), "http://example.com/kernel");
    }

    [Fact]
    public async Task ItSupportsArgumentsAsIsWithoutConvertingTheirTypeAsync()
    {
        //Arrange
        async Task AssertParameterType<T>(T expected)
        {
            var d = (T actual) =>
            {
                //Check the argument is of the expected type
                if (actual is not null)
                {
                    Assert.IsType<T>(actual);
                }

                //Check the argument value is the expected value
                Assert.Equal(expected, actual);
            };

            var arguments = new KernelArguments() { { "actual", (T)expected } };

            await KernelFunctionFactory.CreateFromMethod(d, functionName: "Test")!.InvokeAsync(this._kernel, arguments);
        }

        // Act & Assert
        await AssertParameterType<string?>(null);
        await AssertParameterType<string>("2");
        await AssertParameterType<byte>(2);
        await AssertParameterType<sbyte>(2);
        await AssertParameterType<short>(2);
        await AssertParameterType<ushort>(2);
        await AssertParameterType<int>(2);
        await AssertParameterType<uint>(2);
        await AssertParameterType<long>(2);
        await AssertParameterType<ulong>(2);
        await AssertParameterType<float>(2);
        await AssertParameterType<double>(2);
        await AssertParameterType<decimal>(2);
        await AssertParameterType<char>('w');
        await AssertParameterType<bool>(true);
        await AssertParameterType<DateTime>(DateTime.UtcNow);
        await AssertParameterType<DateTimeOffset>(DateTimeOffset.UtcNow);
        await AssertParameterType<TimeSpan>(TimeSpan.FromMinutes(1));
        await AssertParameterType<Guid>(Guid.NewGuid());
        await AssertParameterType<ConsoleColor>(ConsoleColor.Blue);
        await AssertParameterType<Uri>(new Uri("https://fake-random-test-host/fake-path"));
        await AssertParameterType<object>(new object());
    }

    [Fact]
    public async Task ItSupportsArgumentsImplicitConversionAsync()
    {
        //Arrange
        var arguments = new KernelArguments()
        {
            ["l"] = (int)1,                 //Passed to parameter of type long
            ["i"] = (byte)1,                //Passed to parameter of type int
            ["d"] = (float)1.0,             //Passed to parameter of type double
            ["f"] = (uint)1.0,              //Passed to parameter of type float
            ["g"] = new Guid("35626209-b0ab-458c-bfc4-43e6c7bd13dc"),   //Passed to parameter of type string
            ["dof"] = DayOfWeek.Thursday    //Passed to parameter of type int
        };

        var function = KernelFunctionFactory.CreateFromMethod((long l, int i, double d, float f, string g, int dof) =>
        {
            Assert.Equal(1, l);
            Assert.Equal(1, i);
            Assert.Equal(1.0, d);
            Assert.Equal("35626209-b0ab-458c-bfc4-43e6c7bd13dc", g);
            Assert.Equal(4, dof);
        },
        functionName: "Test");

        // Act & Assert
        await function.InvokeAsync(this._kernel, arguments);
    }

    [Fact]
    public async Task ItSupportsParametersWithDefaultValuesAsync()
    {
        //Arrange

        static void Test(int a, long b = 20, string c = "dv", char d = 'w')
        {
            Assert.Equal(10, a);
            Assert.Equal(20, b);
            Assert.Equal("dv", c);
            Assert.Equal('w', d);
        }

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);

        await function.InvokeAsync(this._kernel, arguments: new() { { "a", 10 } }); // Passing value for the 'a' parameter only.
    }

    [Fact]
    public async Task ItShouldMarshalArgumentsOfValueTypeAsync()
    {
        //Scenario #1 - passing int argument to a method that accepts int
        object? actual = null;
        var sut = KernelFunctionFactory.CreateFromMethod((int val) => { actual = val; });
        await sut.InvokeAsync(this._kernel, new() { ["val"] = 5 });
        Assert.Equal(5, actual);

        //Scenario #2 - passing null argument to a method that accepts int
        sut = KernelFunctionFactory.CreateFromMethod((int val) => { actual = val; });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.InvokeAsync(this._kernel, new() { ["val"] = null }));

        //Scenario #3 - passing int argument to a method that accepts int?
        actual = null;
        sut = KernelFunctionFactory.CreateFromMethod((int? val) => { actual = val; });
        await sut.InvokeAsync(this._kernel, new() { ["val"] = 5 });
        Assert.Equal(5, actual);

        //Scenario #4 - passing null argument to a method that accepts int?
        actual = new();
        sut = KernelFunctionFactory.CreateFromMethod((int? val) => { actual = val; });
        await sut.InvokeAsync(this._kernel, new() { ["val"] = null });
        Assert.Null(actual);
    }

    [Fact]
    public async Task ItShouldMarshalArgumentsOfReferenceTypeAsync()
    {
        //Scenario #1 - passing string argument to a method that accepts string
        object? actual = null;
        var sut = KernelFunctionFactory.CreateFromMethod((string val) => { actual = val; });
        await sut.InvokeAsync(this._kernel, new() { ["val"] = "5" });
        Assert.Equal("5", actual);

        //Scenario #2 - passing null argument to a method that accepts string
        actual = new();
        sut = KernelFunctionFactory.CreateFromMethod((string val) => { actual = val; });
        await sut.InvokeAsync(this._kernel, new() { ["val"] = null });
        Assert.Null(actual);

        //Scenario #3 - passing string argument to a method that accepts string?
        actual = null;
        sut = KernelFunctionFactory.CreateFromMethod((string? val) => { actual = val; });
        await sut.InvokeAsync(this._kernel, new() { ["val"] = "5" });
        Assert.Equal("5", actual);

        //Scenario #4 - passing null argument to a method that accepts string?
        actual = new();
        sut = KernelFunctionFactory.CreateFromMethod((string? val) => { actual = val; });
        await sut.InvokeAsync(this._kernel, new() { ["val"] = null });
        Assert.Null(actual);
    }

    [Fact]
    public async Task ItUsesContextCultureForParsingFormattingAsync()
    {
        // Arrange
        var arguments = new KernelArguments();
        KernelFunction func = KernelFunctionFactory.CreateFromMethod((double input) => input * 2, functionName: "Test");
        FunctionResult result;

        // Act/Assert

        this._kernel.Culture = new CultureInfo("fr-FR");
        arguments[KernelArguments.InputParameterName] = "12,34"; // tries first to parse with the specified culture
        result = await func.InvokeAsync(this._kernel, arguments);
        Assert.Equal(24.68, result.GetValue<double>());
        Assert.Equal("24,68", result.ToString());

        this._kernel.Culture = new CultureInfo("fr-FR");
        arguments[KernelArguments.InputParameterName] = "12.34"; // falls back to invariant culture
        result = await func.InvokeAsync(this._kernel, arguments);
        Assert.Equal(24.68, result.GetValue<double>());
        Assert.Equal("24,68", result.ToString());

        this._kernel.Culture = new CultureInfo("en-US");
        arguments[KernelArguments.InputParameterName] = "12.34"; // works with current culture
        result = await func.InvokeAsync(this._kernel, arguments);
        Assert.Equal(24.68, result.GetValue<double>());
        Assert.Equal("24.68", result.ToString());

        this._kernel.Culture = new CultureInfo("en-US");
        arguments[KernelArguments.InputParameterName] = "12,34"; // not parsable with current or invariant culture
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => func.InvokeAsync(this._kernel, arguments));
    }

    [Fact]
    public async Task ItThrowsWhenItFailsToConvertAnArgumentAsync()
    {
        static string Test(Guid g) => g.ToString();

        var arguments = new KernelArguments();
        arguments["g"] = "7e08cc00-1d71-4558-81ed-69929499dxyz";

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);
        Assert.NotNull(function);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => function.InvokeAsync(this._kernel, arguments));

        //Assert
        AssertExtensions.AssertIsArgumentOutOfRange(ex, "g", (string?)arguments["g"]!);
    }

    [Fact]
    public void ItExposesMetadataFromDelegate()
    {
        [Description("Concat information")]
        static string Test(Guid id, string name, int old) => $"{id} {name} {old}";

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);

        // Assert
        Assert.Contains("Test", function.Name, StringComparison.Ordinal);
        Assert.Equal("Concat information", function.Description);
        Assert.Equal("id", function.Metadata.Parameters[0].Name);
        Assert.Equal("name", function.Metadata.Parameters[1].Name);
        Assert.Equal("old", function.Metadata.Parameters[2].Name);
    }

    [Fact]
    public void ItExposesMetadataFromMethodInfo()
    {
        [Description("Concat information")]
        static string Test(Guid id, string name, int old) => $"{id} {name} {old}";

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Test);

        // Assert
        Assert.Contains("Test", function.Name, StringComparison.Ordinal);
        Assert.Equal("Concat information", function.Description);
        Assert.Equal("id", function.Metadata.Parameters[0].Name);
        Assert.Equal("name", function.Metadata.Parameters[1].Name);
        Assert.Equal("old", function.Metadata.Parameters[2].Name);
    }

    [Fact]
    public async Task ItCanReturnBasicTypesAsync()
    {
        // Arrange
        static int TestInt(int number) => number;
        static double TestDouble(double number) => number;
        static string TestString(string str) => str;
        static bool TestBool(bool flag) => flag;

        var function1 = KernelFunctionFactory.CreateFromMethod(TestInt);
        var function2 = KernelFunctionFactory.CreateFromMethod(TestDouble);
        var function3 = KernelFunctionFactory.CreateFromMethod(TestString);
        var function4 = KernelFunctionFactory.CreateFromMethod(TestBool);

        // Act
        FunctionResult result1 = await function1.InvokeAsync(this._kernel, new("42"));
        FunctionResult result2 = await function2.InvokeAsync(this._kernel, new("3.14"));
        FunctionResult result3 = await function3.InvokeAsync(this._kernel, new("test-string"));
        FunctionResult result4 = await function4.InvokeAsync(this._kernel, new("true"));

        // Assert
        Assert.Equal(42, result1.GetValue<int>());
        Assert.Equal("42", result1.ToString());

        Assert.Equal(3.14, result2.GetValue<double>());
        Assert.Equal("3.14", result2.ToString());

        Assert.Equal("test-string", result3.GetValue<string>());
        Assert.Equal("test-string", result3.ToString());

        Assert.True(result4.GetValue<bool>());
        Assert.Equal("True", result4.ToString());
    }

    [Fact]
    public async Task ItCanReturnComplexTypeAsync()
    {
        // Arrange
        static MyCustomType TestCustomType(MyCustomType instance) => instance;

        var arguments = new KernelArguments();
        arguments["instance"] = "42";

        var function = KernelFunctionFactory.CreateFromMethod(TestCustomType);

        // Act
        FunctionResult result = await function.InvokeAsync(this._kernel, arguments);

        var actualInstance = result.GetValue<MyCustomType>();

        // Assert
        Assert.NotNull(actualInstance);
        Assert.Equal(42, result.GetValue<MyCustomType>()?.Value);
        Assert.Equal(42, actualInstance.Value);
    }

    [Fact]
    public async Task ItCanReturnAsyncEnumerableTypeAsync()
    {
        // Arrange
        static async IAsyncEnumerable<int> TestAsyncEnumerableTypeAsync()
        {
            yield return 1;

            await Task.Delay(50);

            yield return 2;

            await Task.Delay(50);

            yield return 3;
        }

        var function = KernelFunctionFactory.CreateFromMethod(TestAsyncEnumerableTypeAsync);

        // Act
        FunctionResult result = await function.InvokeAsync(this._kernel, new KernelArguments());

        // Assert
        Assert.NotNull(result);

        var asyncEnumerableResult = result.GetValue<IAsyncEnumerable<int>>();

        Assert.NotNull(asyncEnumerableResult);

        var assertResult = new List<int>();

        await foreach (var value in asyncEnumerableResult)
        {
            assertResult.Add(value);
        }

        Assert.True(assertResult.SequenceEqual(new List<int> { 1, 2, 3 }));
    }

    [Fact]
    public async Task ItPropagatesOriginalExceptionTypeAsync()
    {
        // Arrange
        var arguments = new KernelArguments();
        Exception expected = new FormatException("expected");
        KernelFunction func = KernelFunctionFactory.CreateFromMethod(() => { throw expected; });

        // Act
        Exception actual = await Record.ExceptionAsync(() => func.InvokeAsync(this._kernel, arguments));

        // Assert
        Assert.Same(expected, actual);
    }

    public class ComplexType
    {
        public int Foo { get; set; }
    }

    [Fact]
    public async Task ItSupportsMarshallingStringToComplexTypeAsync()
    {
        //Arrange
        static ComplexType Test(ComplexType complexType) => complexType;

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Method(Test));
        FunctionResult result = await function.InvokeAsync(this._kernel, arguments: new() { { "complexType", @"{ ""Foo"": 42 }" } });

        // Assert
        Assert.NotNull(result);

        var complexTypeResult = result.GetValue<ComplexType>();
        Assert.NotNull(complexTypeResult);
        Assert.Equal(42, complexTypeResult.Foo);
    }

    [Fact]
    public async Task ItSupportsMarshallingComplexTypeToStringAsync()
    {
        //Arrange
        static ComplexType Test(ComplexType complexType) => complexType;

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Method(Test));
        FunctionResult result = await function.InvokeAsync(this._kernel, arguments: new() { { "complexType", new ComplexType() { Foo = 42 } } });

        // Assert        
        Assert.NotNull(result);

        var stringResult = result.ToString();
        Assert.NotNull(stringResult);
        Assert.Equal(@"{""Foo"":42}", stringResult);
    }

    [Fact]
    public async Task ItThrowsWhenMarshallingComplexTypeToNonStringTypeAsync()
    {
        //Arrange
        static int Test(int complexType) => complexType;

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Method(Test));

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => await function.InvokeAsync(this._kernel, arguments: new() { { "complexType", new ComplexType() { Foo = 42 } } }));
    }

    [Fact]
    public async Task ItThrowsWhenMarshallingNonStringTypeToComplexTypeAsync()
    {
        //Arrange
        static ComplexType Test(ComplexType complexType) => complexType;

        // Act
        var function = KernelFunctionFactory.CreateFromMethod(Method(Test));
        FunctionResult result = await function.InvokeAsync(this._kernel, arguments: new() { { "complexType", new ComplexType() { Foo = 42 } } });

        // Assert
        Assert.NotNull(result);
        Assert.Throws<InvalidCastException>(() => result.GetValue<double>());
    }
}
