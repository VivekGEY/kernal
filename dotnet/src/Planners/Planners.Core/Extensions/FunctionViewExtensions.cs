﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Json.Schema;
using Microsoft.SemanticKernel.Planners.Model;

#pragma warning disable IDE0130
namespace Microsoft.SemanticKernel.Planners;
#pragma warning restore IDE0130

/// <summary>
/// Provides extension methods for the <see cref="FunctionView"/> class.
/// </summary>
internal static class FunctionViewExtensions
{
    /// <summary>
    /// Create a manual-friendly string for a function.
    /// </summary>
    /// <param name="function">The function to create a manual-friendly string for.</param>
    /// <returns>A manual-friendly string for a function.</returns>
    internal static string ToManualString(this FunctionView function)
    {
        var inputs = string.Join("\n", function.Parameters.Select(parameter =>
        {
            var defaultValueString = string.IsNullOrEmpty(parameter.DefaultValue) ? string.Empty : $" (default value: {parameter.DefaultValue})";
            return $"    - {parameter.Name}: {parameter.Description}{defaultValueString}";
        }));

        // description and inputs are indented by 2 spaces
        // While each parameter in inputs is indented by 4 spaces
        return $@"{function.ToFullyQualifiedName()}:
  description: {function.Description}
  inputs:
{inputs}";
    }

    internal static JsonSchemaFunctionManual ToJsonSchemaManual(this FunctionView function, bool includeOutputSchema = true)
    {
        var functionManual = new JsonSchemaFunctionManual
        {
            Name = function.Name,
            Description = function.Description,
        };

        var requiredProperties = new List<string>();
        foreach (var parameter in function.Parameters)
        {
            if (parameter.NativeType != null)
            {

            }
            else if (parameter.Schema != null)
            {
                functionManual.Parameters.Properties.Add(parameter.Name, parameter.Schema);
                if (parameter.IsRequired ?? false)
                {
                    requiredProperties.Add(parameter.Name);
                }
            }
        }

        return functionManual;
    }

    /// <summary>
    /// Create a fully qualified name for a function.
    /// </summary>
    /// <param name="function">The function to create a fully qualified name for.</param>
    /// <returns>A fully qualified name for a function.</returns>
    internal static string ToFullyQualifiedName(this FunctionView function)
    {
        return $"{function.PluginName}.{function.Name}";
    }

    /// <summary>
    /// Create a string for generating an embedding for a function.
    /// </summary>
    /// <param name="function">The function to create a string for generating an embedding for.</param>
    /// <returns>A string for generating an embedding for a function.</returns>
    internal static string ToEmbeddingString(this FunctionView function)
    {
        var inputs = string.Join("\n", function.Parameters.Select(p => $"    - {p.Name}: {p.Description}"));
        return $"{function.Name}:\n  description: {function.Description}\n  inputs:\n{inputs}";
    }
}
