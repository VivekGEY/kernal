﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Represents the base class for different function choice behaviors.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AutoFunctionChoiceBehavior), typeDiscriminator: "auto")]
[JsonDerivedType(typeof(RequiredFunctionChoiceBehavior), typeDiscriminator: "required")]
[JsonDerivedType(typeof(NoneFunctionChoiceBehavior), typeDiscriminator: "none")]
[Experimental("SKEXP0001")]
public abstract class FunctionChoiceBehavior
{
    /// <summary>The separator used to separate plugin name and function name.</summary>
    protected const string FunctionNameSeparator = ".";

    /// <summary>
    /// Creates a new instance of the <see cref="FunctionChoiceBehavior"/> class.
    /// </summary>
    internal FunctionChoiceBehavior()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="FunctionChoiceBehavior"/> that provides either all of the <see cref="Kernel"/>'s plugins' function information to the model or a specified subset.
    /// This behavior allows the model to decide whether to call the functions and, if so, which ones to call.
    /// </summary>
    /// <param name="functions">The subset of the <see cref="Kernel"/>'s plugins' functions to provide to the model.
    /// If null or empty, all <see cref="Kernel"/>'s plugins' functions are provided to the model.</param>
    /// <param name="autoInvoke">Indicates whether the functions should be automatically invoked by the AI service/connector.</param>
    /// <returns>An instance of one of the <see cref="FunctionChoiceBehavior"/>.</returns>
    public static FunctionChoiceBehavior Auto(IEnumerable<KernelFunction>? functions, bool autoInvoke)
    {
        return new AutoFunctionChoiceBehavior(functions, new FunctionChoiceBehaviorOptions() { AutoInvoke = autoInvoke });
    }

    /// <summary>
    /// Gets an instance of the <see cref="FunctionChoiceBehavior"/> that provides either all of the <see cref="Kernel"/>'s plugins' function information to the model or a specified subset.
    /// This behavior allows the model to decide whether to call the functions and, if so, which ones to call.
    /// </summary>
    /// <param name="functions">The subset of the <see cref="Kernel"/>'s plugins' functions to provide to the model.
    /// If null or empty, all <see cref="Kernel"/>'s plugins' functions are provided to the model.</param>
    /// <param name="options">The options for the behavior.</param>
    /// <returns>An instance of one of the <see cref="FunctionChoiceBehavior"/>.</returns>
    public static FunctionChoiceBehavior Auto(IEnumerable<KernelFunction>? functions = null, FunctionChoiceBehaviorOptions? options = null)
    {
        return new AutoFunctionChoiceBehavior(functions, options);
    }

    /// <summary>
    /// Gets an instance of the <see cref="FunctionChoiceBehavior"/> that provides either all of the <see cref="Kernel"/>'s plugins' function information to the model or a specified subset.
    /// This behavior forces the model to always call one or more functions. The model will then select which function(s) to call.
    /// </summary>
    /// <param name="functions">The subset of the <see cref="Kernel"/>'s plugins' functions to provide to the model.
    /// If null or empty, all <see cref="Kernel"/>'s plugins' functions are provided to the model.</param>
    /// <param name="autoInvoke">Indicates whether the functions should be automatically invoked by the AI service/connector.</param>
    /// <returns>An instance of one of the <see cref="FunctionChoiceBehavior"/>.</returns>
    public static FunctionChoiceBehavior Required(IEnumerable<KernelFunction>? functions, bool autoInvoke)
    {
        return new RequiredFunctionChoiceBehavior(functions, new FunctionChoiceBehaviorOptions { AutoInvoke = autoInvoke });
    }

    /// <summary>
    /// Gets an instance of the <see cref="FunctionChoiceBehavior"/> that provides either all of the <see cref="Kernel"/>'s plugins' function information to the model or a specified subset.
    /// This behavior forces the model to always call one or more functions. The model will then select which function(s) to call.
    /// </summary>
    /// <param name="functions">The subset of the <see cref="Kernel"/>'s plugins' functions to provide to the model.
    /// If null or empty, all <see cref="Kernel"/>'s plugins' functions are provided to the model.</param>
    /// <param name="options">The options for the behavior.</param>
    /// <returns>An instance of one of the <see cref="FunctionChoiceBehavior"/>.</returns>
    public static FunctionChoiceBehavior Required(IEnumerable<KernelFunction>? functions = null, FunctionChoiceBehaviorOptions? options = null)
    {
        return new RequiredFunctionChoiceBehavior(functions, options);
    }

    /// <summary>
    /// Gets an instance of the <see cref="FunctionChoiceBehavior"/> that provides either all of the <see cref="Kernel"/>'s plugins' function information to the model or a specified subset.
    /// This behavior forces the model to not call any functions and only generate a user-facing message.
    /// </summary>
    /// <param name="functions">The subset of the <see cref="Kernel"/>'s plugins' functions to provide to the model.
    /// If null or empty, all <see cref="Kernel"/>'s plugins' functions are provided to the model.</param>
    /// <param name="autoInvoke">Indicates whether the functions should be automatically invoked by the AI service/connector.</param>
    /// <returns>An instance of one of the <see cref="FunctionChoiceBehavior"/>.</returns>
    /// <remarks>
    /// Although this behavior prevents the model from calling any functions, the model can use the provided function information
    /// to describe how it would complete the prompt if it had the ability to call the functions.
    /// </remarks>
    public static FunctionChoiceBehavior None(IEnumerable<KernelFunction>? functions, bool autoInvoke)
    {
        return new NoneFunctionChoiceBehavior(functions, new FunctionChoiceBehaviorOptions { AutoInvoke = autoInvoke });
    }

    /// <summary>
    /// Gets an instance of the <see cref="FunctionChoiceBehavior"/> that provides either all of the <see cref="Kernel"/>'s plugins' function information to the model or a specified subset.
    /// This behavior forces the model to not call any functions and only generate a user-facing message.
    /// </summary>
    /// <param name="functions">The subset of the <see cref="Kernel"/>'s plugins' functions to provide to the model.
    /// If null or empty, all <see cref="Kernel"/>'s plugins' functions are provided to the model.</param>
    /// <param name="options">The options for the behavior.</param>
    /// <returns>An instance of one of the <see cref="FunctionChoiceBehavior"/>.</returns>
    /// <remarks>
    /// Although this behavior prevents the model from calling any functions, the model can use the provided function information
    /// to describe how it would complete the prompt if it had the ability to call the functions.
    /// </remarks>
    public static FunctionChoiceBehavior None(IEnumerable<KernelFunction>? functions = null, FunctionChoiceBehaviorOptions? options = null)
    {
        return new NoneFunctionChoiceBehavior(functions, options);
    }

    /// <summary>Returns the configuration specified by the <see cref="FunctionChoiceBehavior"/>.</summary>
    /// <param name="context">The function choice caller context.</param>
    /// <returns>The configuration.</returns>
    public abstract FunctionChoiceBehaviorConfiguration GetConfiguration(FunctionChoiceBehaviorContext context);
}
