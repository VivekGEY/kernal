﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using HandlebarsDotNet;
using HandlebarsDotNet.Compiler;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning.Handlebars.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.SemanticKernel.Planning.Handlebars;
#pragma warning restore IDE0130

/// <summary>
/// Provides extension methods for rendering Handlebars templates in the context of a Semantic Kernel.
/// </summary>
internal sealed class HandlebarsTemplateEngineExtensions
{
    /// <summary>
    /// The key used to store the reserved output type in the dictionary of variables passed to the Handlebars template engine.
    /// </summary>
    public const string ReservedOutputTypeKey = "RESERVED_OUTPUT_TYPE";

    /// <summary>
    /// The character used to delimit the plugin name and function name in a Handlebars template.
    /// </summary>
    public const string ReservedNameDelimiter = "-";

    /// <summary>
    /// Renders a Handlebars template in the context of a Semantic Kernel.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel.</param>
    /// <param name="executionContext">The execution context.</param>
    /// <param name="template">The Handlebars template to render.</param>
    /// <param name="variables">The dictionary of variables to pass to the Handlebars template engine.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered Handlebars template.</returns>
    public static string Render(
        Kernel kernel,
        SKContext executionContext,
        string template,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        IHandlebars handlebarsInstance = HandlebarsDotNet.Handlebars.Create(
            new HandlebarsConfiguration
            {
                NoEscape = true
            });

        // Add helpers for each function
        foreach (SKFunctionMetadata function in kernel.Plugins.GetFunctionsMetadata())
        {
            RegisterFunctionAsHelper(kernel, executionContext, handlebarsInstance, function, variables, cancellationToken);
        }

        // Add system helpers
        RegisterSystemHelpers(handlebarsInstance, variables);

        var compiledTemplate = handlebarsInstance.Compile(template);
        return compiledTemplate(variables);
    }

    private static void RegisterFunctionAsHelper(
        Kernel kernel,
        SKContext executionContext,
        IHandlebars handlebarsInstance,
        SKFunctionMetadata functionMetadata,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        string fullyResolvedFunctionName = functionMetadata.PluginName + ReservedNameDelimiter + functionMetadata.Name;

        handlebarsInstance.RegisterHelper(fullyResolvedFunctionName, (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            // Get the parameters from the template arguments
            if (arguments.Any())
            {
                if (arguments[0].GetType() == typeof(HashParameterDictionary))
                {
                    // Process hash arguments
                    var handlebarArgs = arguments[0] as IDictionary<string, object>;

                    // Prepare the input parameters for the function
                    foreach (var param in functionMetadata.Parameters)
                    {
                        var fullyQualifiedParamName = functionMetadata.Name + ReservedNameDelimiter + param.Name;
                        var value = handlebarArgs is not null && (handlebarArgs.TryGetValue(param.Name, out var val) || handlebarArgs.TryGetValue(fullyQualifiedParamName, out val)) ? val : null;

                        if (value is not null && (handlebarArgs?.ContainsKey(param.Name) == true || handlebarArgs?.ContainsKey(fullyQualifiedParamName) == true))
                        {
                            variables[param.Name] = value;
                        }
                        else if (param.IsRequired == true)
                        {
                            throw new SKException($"Parameter {param.Name} is required for function {functionMetadata.Name}.");
                        }
                    }
                }
                else
                {
                    // Process positional arguments
                    var requiredParameters = functionMetadata.Parameters.Where(p => p.IsRequired == true).ToList();
                    if (arguments.Length >= requiredParameters.Count && arguments.Length <= functionMetadata.Parameters.Count)
                    {
                        var argIndex = 0;
                        foreach (var arg in arguments)
                        {
                            var param = functionMetadata.Parameters[argIndex];
                            if (param.Type == null || arg.GetType() == typeof(object) || IsExpectedParameterType(param.Type.ToString(), arg.GetType().Name, arg))
                            {
                                variables[param.Name] = arguments[argIndex];
                                argIndex++;
                            }
                            else
                            {
                                throw new SKException($"Invalid parameter type for function {functionMetadata.Name}. Parameter {param.Name} expects type {param.Type} but received {arguments[argIndex].GetType()}.");
                            }
                        }
                    }
                    else
                    {
                        throw new SKException($"Invalid parameter count for function {functionMetadata.Name}. {arguments.Length} were specified but {functionMetadata.Parameters.Count} are required.");
                    }
                }
            }

            foreach (var v in variables)
            {
                var value = v.Value ?? "";
                var varString = !SKParameterMetadataExtensions.isPrimitiveOrStringType(value.GetType()) ? JsonSerializer.Serialize(value) : value.ToString();
                if (executionContext.Variables.TryGetValue(v.Key, out var argVal))
                {
                    executionContext.Variables[v.Key] = varString;
                }
                else
                {
                    executionContext.Variables.Add(v.Key, varString);
                }
            }

            // TODO (@teresaqhoang): Add model results to execution context + test possible deadlock scenario
            ISKFunction function = kernel.Plugins.GetFunction(functionMetadata.PluginName, functionMetadata.Name);

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            KernelResult result = kernel.RunAsync(executionContext.Variables, cancellationToken, function).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

            var returnType = function.GetMetadata().ReturnParameter.ParameterType;
            var resultAsObject = result.GetValue<object?>();

            // If return type is complex, serialize the object so it can be deserialized with appropriate keys to capture expected class properties.
            // i.e., class properties can be different if JsonPropertyName = 'id' and class property is 'Id'.
            if (returnType is not null && !SKParameterMetadataExtensions.isPrimitiveOrStringType(returnType))
            {
                var serializedResult = JsonSerializer.Serialize(resultAsObject);
                resultAsObject = JsonSerializer.Deserialize(serializedResult, returnType);
            }

            // Write the result to the template
            return resultAsObject;
        });
    }

    private static void RegisterSystemHelpers(
        IHandlebars handlebarsInstance,
        Dictionary<string, object?> variables
    )
    {
        // Not exposed as a helper to the model, used for initial prompt rendering only.
        handlebarsInstance.RegisterHelper("or", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var isAtLeastOneTruthy = false;
            foreach (var arg in arguments)
            {
                if (arg is not null)
                {
                    isAtLeastOneTruthy = true;
                }
            }

            return isAtLeastOneTruthy;
        });

        handlebarsInstance.RegisterHelper("getSchemaTypeName", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            SKParameterMetadata parameter = (SKParameterMetadata)arguments[0];
            return parameter.GetSchemaTypeName();
        });

        handlebarsInstance.RegisterHelper("getSchemaReturnTypeName", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            SKReturnParameterMetadata parameter = (SKReturnParameterMetadata)arguments[0];
            var functionName = arguments[1].ToString();
            return parameter.ToSKParameterMetadata(functionName).GetSchemaTypeName();
        });

        handlebarsInstance.RegisterHelper("array", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            // Convert all the arguments to an array
            var array = arguments.Select(a => a).ToList();

            return array;
        });

        handlebarsInstance.RegisterHelper("range", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var start = int.Parse(arguments[0].ToString(), CultureInfo.InvariantCulture);
            var end = int.Parse(arguments[1].ToString(), CultureInfo.InvariantCulture) + 1;

            var count = end - start;

            // Create array from start to end
            var array = Enumerable.Range(start, count).ToList();

            return array;
        });

        handlebarsInstance.RegisterHelper("concat", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            List<string?> strings = arguments.Select((var) =>
            {
                if (var == null)
                {
                    return null;
                }
                return var!.ToString();
            }).ToList();
            return string.Concat(strings);
        });

        handlebarsInstance.RegisterHelper("add", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            double left = CastToNumber(arguments[0]);
            double right = CastToNumber(arguments[1]);

            return left + right;
        });

        handlebarsInstance.RegisterHelper("subtract", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            double left = CastToNumber(arguments[0]);
            double right = CastToNumber(arguments[1]);

            return right - left;
        });

        handlebarsInstance.RegisterHelper("equal", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            object? left = arguments[0];
            object? right = arguments[1];

            return left == right || (left is not null && left.Equals(right));
        });

        handlebarsInstance.RegisterHelper("lessThan", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            double left = CastToNumber(arguments[0]);
            double right = CastToNumber(arguments[1]);

            return left < right;
        });

        handlebarsInstance.RegisterHelper("greaterThan", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            double left = CastToNumber(arguments[0]);
            double right = CastToNumber(arguments[1]);

            return left > right;
        });

        handlebarsInstance.RegisterHelper("lessThanOrEqual", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            double left = CastToNumber(arguments[0]);
            double right = CastToNumber(arguments[1]);

            return left <= right;
        });

        handlebarsInstance.RegisterHelper("greaterThanOrEqual", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            double left = CastToNumber(arguments[0]);
            double right = CastToNumber(arguments[1]);

            return left >= right;
        });

        handlebarsInstance.RegisterHelper("json", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            object objectToSerialize = arguments[0];
            string json = objectToSerialize.GetType() == typeof(string) ? (string)objectToSerialize : JsonSerializer.Serialize(objectToSerialize);

            return json;
        });

        handlebarsInstance.RegisterHelper("message", (writer, options, context, arguments) =>
        {
            var parameters = arguments[0] as IDictionary<string, object>;

            // Verify that the message has a role
            if (!parameters!.ContainsKey("role"))
            {
                throw new SKException("Message must have a role.");
            }

            writer.Write($"<{parameters["role"]}~>", false);
            options.Template(writer, context);
            writer.Write($"</{parameters["role"]}~>", false);
        });

        handlebarsInstance.RegisterHelper("raw", (writer, options, context, arguments) =>
        {
            options.Template(writer, null);
        });

        handlebarsInstance.RegisterHelper("doubleOpen", (writer, context, arguments) =>
        {
            writer.Write("{{");
        });

        handlebarsInstance.RegisterHelper("doubleClose", (writer, context, arguments) =>
        {
            writer.Write("}}");
        });

        handlebarsInstance.RegisterHelper("set", (writer, context, arguments) =>
        {
            var name = string.Empty;
            object value = string.Empty;
            if (arguments[0].GetType() == typeof(HashParameterDictionary))
            {
                // Get the parameters from the template arguments
                var parameters = arguments[0] as IDictionary<string, object>;
                name = (string)parameters!["name"];
                value = parameters!["value"];
            }
            else
            {
                name = arguments[0].ToString();
                value = arguments[1];
            }

            if (variables.TryGetValue(name, out var argVal))
            {
                variables[name] = value;
            }
            else
            {
                variables.Add(name, value);
            }
        });

        handlebarsInstance.RegisterHelper("get", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            if (arguments[0].GetType() == typeof(HashParameterDictionary))
            {
                var parameters = arguments[0] as IDictionary<string, object>;
                return variables[(string)parameters!["name"]];
            }

            return variables[arguments[0].ToString()];
        });
    }

    private static bool IsNumericType(string typeStr)
    {
        Type? type = string.IsNullOrEmpty(typeStr) ? Type.GetType(typeStr) : null;

        if (type == null)
        {
            return false;
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.Decimal or TypeCode.Double or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.SByte or TypeCode.Single or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => true,
            _ => false,
        };
    }

    private static bool TryParseAnyNumber(string input)
    {
        // Check if input can be parsed as any of these numeric types  
        return int.TryParse(input, out _)
            || double.TryParse(input, out _)
            || float.TryParse(input, out _)
            || long.TryParse(input, out _)
            || decimal.TryParse(input, out _)
            || short.TryParse(input, out _)
            || byte.TryParse(input, out _)
            || sbyte.TryParse(input, out _)
            || ushort.TryParse(input, out _)
            || uint.TryParse(input, out _)
            || ulong.TryParse(input, out _);
    }

    private static double CastToNumber(object? number)
    {
        if (number is int numberInt)
        {
            return numberInt;
        }
        else if (number is decimal numberDecimal)
        {
            return (double)numberDecimal;
        }
        else
        {
            return double.Parse(number!.ToString()!, CultureInfo.InvariantCulture);
        }
    }

    /*
     * Type check will pass if:
     * Types are an exact match.
     * Handlebar argument is any kind of numeric type if function parameter requires a numeric type.
     * Handlebar argument type is an object (this covers complex types).
     * Function parameter is a generic type.
     */
    private static bool IsExpectedParameterType(string functionMetadataType, string handlebarArgumentType, object handlebarArgValue)
    {
        var isValidNumericType = IsNumericType(functionMetadataType) && IsNumericType(handlebarArgumentType);
        if (IsNumericType(functionMetadataType) && !IsNumericType(handlebarArgumentType))
        {
            isValidNumericType = TryParseAnyNumber(handlebarArgValue.ToString());
        }

        return functionMetadataType == handlebarArgumentType || isValidNumericType || handlebarArgumentType == "object";
    }
}
