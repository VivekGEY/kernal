﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text.Json;

namespace Microsoft.SemanticKernel.Functions.OpenAPI.Model;

/// <summary>
/// The REST API operation response.
/// </summary>
[TypeConverterAttribute(typeof(RestApiOperationResponseConverter))]
public sealed class RestApiOperationResponse
{
    /// <summary>
    /// Gets the content of the response.
    /// </summary>
    public object Content { get; }

    /// <summary>
    /// Gets the content type of the response.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// The expected schema of the response as advertised in the OpenAPI operation.
    /// </summary>
    public JsonDocument? ExpectedSchema { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RestApiOperationResponse"/> class.
    /// </summary>
    /// <param name="content">The content of the response.</param>
    /// <param name="contentType">The content type of the response.</param>
    /// <param name="expectedSchema">The schema against which the response body should be validated.</param>
    public RestApiOperationResponse(object content, string contentType, JsonDocument? expectedSchema = null)
    {
        this.Content = content;
        this.ContentType = contentType;
        this.ExpectedSchema = expectedSchema;
    }
}
