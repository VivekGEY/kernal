﻿// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Exception thrown when a failure occurs while trying to convert memory models for storage or retrieval.
/// </summary>
public class MemoryModelException : KernelException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryModelException"/> class.
    /// </summary>
    public MemoryModelException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryModelException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public MemoryModelException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryModelException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public MemoryModelException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
