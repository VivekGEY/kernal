﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.SemanticKernel.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Connectors.Memory.CosmosDB;

/// <summary>
/// A CosmosDB memory record.
/// </summary>
[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class CosmosDBMemoryRecord
{
    /// <summary>
    /// Unique identifier of the memory record.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier of the collection.
    /// </summary>
    public string CollectionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional timestamp.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// The embedding data as a string.
    /// </summary>
    public string EmbeddingString { get; set; } = string.Empty;

    /// <summary>
    /// Metadata as a string.
    /// </summary>
    public string MetadataString { get; set; } = string.Empty;
}

