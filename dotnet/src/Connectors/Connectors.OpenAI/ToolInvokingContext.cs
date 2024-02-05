﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;
public sealed class ToolInvokingContext : ToolFilterContext
{
    public ToolInvokingContext(KernelFunction function, KernelArguments arguments, int iteration)
    : base(function, arguments, iteration, metadata: null)
    {
    }
}
