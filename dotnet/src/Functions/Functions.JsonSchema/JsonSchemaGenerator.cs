﻿// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Text.Json;
using Json.More;
using Json.Schema;
using Json.Schema.Generation;
using Microsoft.SemanticKernel.Schema;

namespace Microsoft.SemanticKernel.Functions.JsonSchema;

/// <summary>
/// Class for generating JSON Schema from a type.
/// </summary>
public class JsonSchemaGenerator : IJsonSchemaGenerator
{
    /// <inheritdoc/>
    public JsonDocument? GenerateSchema(Type type, string description)
    {
        return new JsonSchemaBuilder()
                .FromType(type)
                .Description(description ?? string.Empty)
                .Build()
                .ToJsonDocument();
    }
}
