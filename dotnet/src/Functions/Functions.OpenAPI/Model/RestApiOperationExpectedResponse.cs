﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;

namespace Microsoft.SemanticKernel.Functions.OpenAPI.Model;

/// <summary>
/// The REST API operation response.
/// </summary>
public sealed class RestApiOperationExpectedResponse
{
    /// <summary>
    /// Gets the content of the response.
    /// </summary>
    public object Description { get; }

    /// <summary>
    /// Gets the media type of the response.
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// The schema of the response.
    /// </summary>
    public JsonDocument? Schema { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RestApiOperationResponse"/> class.
    /// </summary>
    /// <param name="description">The description of the response.</param>
    /// <param name="mediaType">The media type of the response.</param>
    /// <param name="schema">The schema against which the response body should be validated.</param>
    public RestApiOperationExpectedResponse(string description, string mediaType, JsonDocument? schema = null)
    {
        this.Description = description;
        this.MediaType = mediaType;
        this.Schema = schema;
    }
}
