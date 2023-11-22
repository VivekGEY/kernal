﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Services;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides state for use throughout a Semantic Kernel workload.
/// </summary>
/// <remarks>
/// An instance of <see cref="Kernel"/> is passed through to every function invocation and service call
/// throughout the system, providing to each the ability to access shared state and services.
/// </remarks>
public sealed class Kernel
{
    /// <summary>
    /// Gets the culture currently associated with this context.
    /// </summary>
    /// <remarks>
    /// The culture defaults to <see cref="CultureInfo.CurrentCulture"/> if not explicitly set.
    /// It may be set to another culture, such as <see cref="CultureInfo.InvariantCulture"/>,
    /// and any functions invoked within the context can consult this property for use in
    /// operations like formatting and parsing.
    /// </remarks>
    [AllowNull]
    public CultureInfo Culture
    {
        get => this._culture;
        set => this._culture = value ?? CultureInfo.CurrentCulture;
    }

    /// <summary>
    /// Gets the <see cref="ILoggerFactory"/> to use for logging.
    /// </summary>
    /// <remarks>
    /// If no logging is provided, this will be an instance that ignores all logging operations.
    /// </remarks>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets the collection of plugins available through the kernel.
    /// </summary>
    public ISKPluginCollection Plugins =>
        this._plugins ??
        Interlocked.CompareExchange(ref this._plugins, new SKPluginCollection(), null) ??
        this._plugins;

    /// <summary>
    /// Gets the service provider used to query for services available through the kernel.
    /// </summary>
    public IAIServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the <see cref="IAIServiceSelector"/> used to select between multiple AI services.
    /// </summary>
    internal IAIServiceSelector ServiceSelector =>
        this._serviceSelector ??
        Interlocked.CompareExchange(ref this._serviceSelector, new OrderedIAIServiceSelector(), null) ??
        this._serviceSelector;

    /// <summary>
    /// Gets the <see cref="IDelegatingHandlerFactory"/> to use when constructing <see cref="HttpClient"/>
    /// instances for use in HTTP requests.
    /// </summary>
    /// <remarks>
    /// This is typically only used as part of creating plugins and functions, as that is typically
    /// when such clients are constructed.
    /// </remarks>
    public IDelegatingHandlerFactory HttpHandlerFactory { get; }

    /// <summary>
    /// Provides an event that's raised prior to a function's invocation.
    /// </summary>
    public event EventHandler<FunctionInvokingEventArgs>? FunctionInvoking;

    /// <summary>
    /// Provides an event that's raised after a function's invocation.
    /// </summary>
    public event EventHandler<FunctionInvokedEventArgs>? FunctionInvoked;

    /// <summary>
    /// Initializes a new instance of <see cref="Kernel"/>.
    /// </summary>
    /// <param name="aiServiceProvider">The <see cref="IAIServiceProvider"/> used to query for services available through the kernel.</param>
    /// <param name="plugins">The collection of plugins available through the kernel. If null, an empty collection will be used.</param>
    /// <param name="serviceSelector">The <see cref="IAIServiceSelector"/> used to select between multiple AI services.</param>
    /// <param name="httpHandlerFactory">The <see cref="IDelegatingHandlerFactory"/> to use when constructing <see cref="HttpClient"/> instances for use in HTTP requests.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <remarks>
    /// The KernelBuilder class provides a fluent API for constructing a <see cref="Kernel"/> instance.
    /// </remarks>
    public Kernel(
        IAIServiceProvider aiServiceProvider,
        ISKPluginCollection? plugins = null,
        IAIServiceSelector? serviceSelector = null,
        IDelegatingHandlerFactory? httpHandlerFactory = null,
        ILoggerFactory? loggerFactory = null)
    {
        Verify.NotNull(aiServiceProvider);

        this.ServiceProvider = aiServiceProvider;
        this._plugins = plugins;
        this._serviceSelector = serviceSelector;
        this.HttpHandlerFactory = httpHandlerFactory ?? NullHttpHandlerFactory.Instance;
        this.LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Create a new instance of a context, linked to the kernel internal state.
    /// </summary>
    /// <param name="variables">Initializes the context with the provided variables</param>
    /// <param name="plugins">Provides a collection of plugins to be available in the new context. By default, it's the full collection from the kernel.</param>
    /// <returns>SK context</returns>
    public SKContext CreateNewContext(
        ContextVariables? variables = null,
        IReadOnlySKPluginCollection? plugins = null)
    {
        return new SKContext(
            variables,
            new EventHandlerWrapper<FunctionInvokingEventArgs>(this.FunctionInvoking),
            new EventHandlerWrapper<FunctionInvokedEventArgs>(this.FunctionInvoked));
    }

    /// <summary>
    /// Gets a configured service from the service provider.
    /// </summary>
    /// <typeparam name="T">Specifies the type of the service being requested.</typeparam>
    /// <param name="name">The name of the registered service. If a name is not provided, the default service for the specified <typeparamref name="T"/> is returned.</param>
    /// <returns>The instance of the service.</returns>
    /// <exception cref="SKException">The specified service was not registered.</exception>
    public T GetService<T>(string? name = null) where T : IAIService =>
        this.ServiceProvider.GetService<T>(name) ??
        throw new SKException($"Service of type {typeof(T)} and name {name ?? "<NONE>"} not registered.");

    /// <summary>
    /// Gets a dictionary for ambient data associated with the kernel.
    /// </summary>
    /// <remarks>
    /// This may be used to flow arbitrary data in and out of operations performed with this kernel instance.
    /// </remarks>
    public IDictionary<string, object?> Data =>
        this._data ??
        Interlocked.CompareExchange(ref this._data, new Dictionary<string, object?>(), null) ??
        this._data;

    #region private ================================================================================

    private Dictionary<string, object?>? _data;
    private CultureInfo _culture = CultureInfo.CurrentCulture;
    private ISKPluginCollection? _plugins;
    private IAIServiceSelector? _serviceSelector;

    #endregion
}
