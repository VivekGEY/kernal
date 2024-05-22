﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.SemanticKernel.Memory;

/// <summary>
/// Optional options when calling <see cref="IVectorDBRecordService{TKey, TDataModel}.RemoveAsync"/>.
/// </summary>
[Experimental("SKEXP0001")]
public class RemoveRecordOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveRecordOptions"/> class.
    /// </summary>
    public RemoveRecordOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveRecordOptions"/> class by cloning the given options.
    /// </summary>
    /// <param name="source">The options to clone</param>
    public RemoveRecordOptions(RemoveRecordOptions source)
    {
        this.CollectionName = source.CollectionName;
    }

    /// <summary>
    /// Get or sets an optional collection name to use for this operation that is different to the default.
    /// </summary>
    public string? CollectionName { get; init; }
}
