﻿#region HEADER

// Copyright (c) Microsoft. All rights reserved.

#endregion

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Connectors.Gemini.Core;

internal sealed class AuthorRoleConverter : JsonConverter<AuthorRole?>
{
    public override AuthorRole? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? role = reader.GetString();
        if (role == null)
        {
            return null;
        }

        if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorRole.User;
        }

        if (role.Equals("model", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorRole.Assistant;
        }

        throw new JsonException($"Unexpected author role: {role}");
    }

    public override void Write(Utf8JsonWriter writer, AuthorRole? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value != AuthorRole.User && value != AuthorRole.Assistant)
        {
            throw new JsonException($"Gemini API doesn't support author role: {value}");
        }

        writer.WriteStringValue(value == AuthorRole.User ? "user" : "model");
    }
}
