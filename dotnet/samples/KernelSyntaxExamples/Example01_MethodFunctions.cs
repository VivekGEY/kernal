﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.SemanticKernel.Plugins.Core;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

public class Example01_MethodFunctions : BaseTest
{
    public Example01_MethodFunctions(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public Task RunAsync()
    {
        this._output.WriteLine("======== Functions ========");

        // Load native plugin
        var text = new TextPlugin();

        // Use function without kernel
        var result = text.Uppercase("ciao!");

        this._output.WriteLine(result);

        return Task.CompletedTask;
    }
}
