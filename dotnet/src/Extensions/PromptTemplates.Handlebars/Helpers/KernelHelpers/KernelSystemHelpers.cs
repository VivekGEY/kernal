﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using HandlebarsDotNet;
using HandlebarsDotNet.Compiler;

namespace Microsoft.SemanticKernel.PromptTemplates.Handlebars.Helpers;

/// <summary>
/// Extension class to register additional helpers as Kernel System helpers.
/// </summary>
internal static class KernelSystemHelpers
{
    /// <summary>
    /// Register all (default) or specific categories of system helpers.
    /// </summary>
    /// <param name="handlebarsInstance">The <see cref="IHandlebars"/>-instance.</param>
    /// <param name="kernel">Kernel instance.</param>
    /// <param name="variables">Dictionary of variables maintained by the Handlebars context.</param>
    /// <param name="options">Handlebars prompt template options.</param>
    public static void Register(
        IHandlebars handlebarsInstance,
        Kernel kernel,
        KernelArguments variables,
        HandlebarsPromptTemplateOptions options)
    {
        RegisterSystemHelpers(handlebarsInstance, kernel, variables);
    }

    /// <summary>
    /// Register all system helpers.
    /// </summary>
    /// <param name="handlebarsInstance">The <see cref="IHandlebars"/>-instance.</param>
    /// <param name="kernel">Kernel instance.</param>
    /// <param name="variables">Dictionary of variables maintained by the Handlebars context.</param>
    /// <exception cref="KernelException">Exception thrown when a message does not contain a defining role.</exception>
    private static void RegisterSystemHelpers(
        IHandlebars handlebarsInstance,
        Kernel kernel,
        KernelArguments variables)
    {
        object? GetArgumentValue(object argument)
        {
            if (argument is UndefinedBindingResult result)
            {
                return variables.TryGetValue(result.Value, out var variable) ? variable : result.Value;
            }

            return argument;
        }

        Arguments ProcessArguments(Arguments arguments)
        {
            var processedArguments = arguments.Select(arg =>
            {
                return GetArgumentValue(arg);
            });

            return new Arguments(processedArguments.ToArray());
        }

        // TODO [@teresaqhoang]: Issue #3947 Isolate Handlebars Kernel System helpers in their own class
        // Should also consider standardizing the naming conventions for these helpers, i.e., 'Message' instead of 'message'
        handlebarsInstance.RegisterHelper("message", static (writer, options, context, arguments) =>
        {
            var parameters = (IDictionary<string, object>)arguments[0];

            // Verify that the message has a role
            if (!parameters!.TryGetValue("role", out object? value))
            {
                throw new KernelException("Message must have a role.");
            }

            writer.Write($"<{value}~>", false);
            options.Template(writer, context);
            writer.Write($"</{value}~>", false);
        });

        handlebarsInstance.RegisterHelper("set", (writer, context, arguments) =>
        {
            var name = string.Empty;
            object? value = string.Empty;
            if (arguments[0].GetType() == typeof(HashParameterDictionary))
            {
                // Get the parameters from the template arguments
                var parameters = (IDictionary<string, object>)arguments[0];
                name = (string)parameters!["name"];
                value = GetArgumentValue(parameters!["value"]);
            }
            else
            {
                var args = ProcessArguments(arguments);
                name = args[0].ToString();
                value = args[1];
            }

            // Set the variable in the Handlebars context
            variables[name] = value;
        });

        handlebarsInstance.RegisterHelper("json", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] is null)
            {
                throw new HandlebarsRuntimeException("`json` helper requires a value to be passed in.");
            }

            var args = ProcessArguments(arguments);
            object objectToSerialize = args[0];
            var type = objectToSerialize.GetType();

            return type == typeof(string) ? objectToSerialize
                : type == typeof(JsonNode) ? objectToSerialize.ToString()
                : JsonSerializer.Serialize(objectToSerialize);
        });

        handlebarsInstance.RegisterHelper("concat", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var args = ProcessArguments(arguments);
            return string.Concat(args);
        });

        handlebarsInstance.RegisterHelper("array", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var args = ProcessArguments(arguments);
            return args.ToArray();
        });

        handlebarsInstance.RegisterHelper("raw", static (writer, options, context, arguments) =>
        {
            options.Template(writer, null);
        });

        handlebarsInstance.RegisterHelper("range", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var args = ProcessArguments(arguments);

            // Create list with numbers from start to end (inclusive)
            var start = int.Parse(args[0].ToString(), kernel.Culture);
            var end = int.Parse(args[1].ToString(), kernel.Culture) + 1;
            var count = end - start;

            return Enumerable.Range(start, count);
        });

        handlebarsInstance.RegisterHelper("or", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var args = ProcessArguments(arguments);
            return args.Any(arg => arg != null && arg is not false);
        });

        handlebarsInstance.RegisterHelper("add", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var args = ProcessArguments(arguments);
            return args.Sum(arg => decimal.Parse(arg.ToString(), kernel.Culture));
        });

        handlebarsInstance.RegisterHelper("subtract", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            var args = ProcessArguments(arguments);
            return args.Aggregate((a, b) => decimal.Parse(a.ToString(), kernel.Culture) - decimal.Parse(b.ToString(), kernel.Culture));
        });

        handlebarsInstance.RegisterHelper("equals", (in HelperOptions options, in Context context, in Arguments arguments) =>
        {
            if (arguments.Length < 2)
            {
                return false;
            }

            var args = ProcessArguments(arguments);
            object? left = args[0];
            object? right = args[1];

            return left == right || (left is not null && left.Equals(right));
        });
    }
}
