﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.SemanticKernel.Planners.Handlebars;

/// <summary>
/// Extension methods for the <see cref="IHandlebarsPlanner"/> interface.
/// </summary>
public static class HandlebarsPlannerExtensions
{
    /// <summary>
    /// Reads the prompt for the given file name.
    /// </summary>
    /// <param name="planner">The handlebars planner.</param>
    /// <param name="fileName">The name of the file to read.</param>
    /// <returns>The content of the file as a string.</returns>
    public static string ReadPrompt(this IHandlebarsPlanner planner, string fileName)
    {
        using var stream = planner.ReadPromptStream(fileName);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the prompt stream for the given file name.
    /// </summary>
    /// <param name="planner">The handlebars planner.</param>
    /// <param name="fileName">The name of the file to read.</param>
    /// <returns>The stream for the given file name.</returns>
    public static Stream ReadPromptStream(this IHandlebarsPlanner planner, string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{planner.GetType().Namespace}.{fileName}";

        return assembly.GetManifestResourceStream(resourceName)!;
    }

    /// <summary>
    /// Start the stopwatch.
    /// </summary>
    public static void StartStopwatch(this IHandlebarsPlanner planner)
    {
        if (planner.Stopwatch.IsRunning)
        {
            throw new InvalidOperationException("Stopwatch is already running.");
        }

        planner.Stopwatch.Start();
    }

    /// <summary>
    /// Stop the stopwatch and return the elapsed time in milliseconds.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static double StopStopwatch(this IHandlebarsPlanner planner)
    {
        if (planner.Stopwatch.IsRunning)
        {
            planner.Stopwatch.Stop();
            return planner.Stopwatch.Elapsed.TotalMilliseconds;
        }

        throw new InvalidOperationException("Stopwatch is not running.");
    }
}
