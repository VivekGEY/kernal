﻿// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using the main namespace
using System.Text.Json;
using System;

namespace Microsoft.SemanticKernel;
#pragma warning restore IDE0130

/// <summary>
/// Class used to copy and export data about function output for planner and related scenarios.
/// </summary>
/// <param name="Description">Function output description</param>
/// <param name="NativeType">The native type. Null if this parameter did not come from a native function.</param>
/// <param name="Schema">The JSON Schema of the type. May be null for native function parameters.</param>
public sealed record ReturnParameterView(
    string? Description = null,
    Type? NativeType = null,
    JsonDocument? Schema = null);
