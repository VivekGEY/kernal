﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.SemanticKernel.Reliability;

/// <summary>
/// A factory for creating instances of <see cref="NullHttpRetryHandler"/>.
/// </summary>
/// <example>
/// To create a new instance of <see cref="NullHttpRetryHandler"/> using the factory:
/// <code>
/// var factory = new NullHttpRetryHandlerFactory();
/// var handler = factory.Create(null);
/// </code>
/// </example>
public class NullHttpRetryHandlerFactory : IDelegatingHandlerFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="NullHttpRetryHandler"/>.
    /// </summary>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    /// <returns>A new instance of <see cref="NullHttpRetryHandler"/>.</returns>
    public DelegatingHandler Create(ILoggerFactory? loggerFactory)
    {
        return new NullHttpRetryHandler();
    }
}

/// <summary>
/// A HTTP retry handler that does not retry.
/// </summary>
/// <remarks>
/// This handler is useful when you want to disable retry functionality in your HTTP requests.
/// </remarks>
public class NullHttpRetryHandler : DelegatingHandler
{
}
