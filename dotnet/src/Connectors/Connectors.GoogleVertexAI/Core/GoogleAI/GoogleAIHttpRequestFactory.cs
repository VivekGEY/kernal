﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net.Http;
using Microsoft.SemanticKernel.Http;

namespace Microsoft.SemanticKernel.Connectors.GoogleVertexAI.Core;

internal sealed class GoogleAIHttpRequestFactory : IHttpRequestFactory
{
    public HttpRequestMessage CreatePost(object requestData, Uri endpoint)
    {
        var httpRequestMessage = HttpRequest.CreatePostRequest(endpoint, requestData);
        httpRequestMessage.Headers.Add("User-Agent", HttpHeaderConstant.Values.UserAgent);
        httpRequestMessage.Headers.Add(HttpHeaderConstant.Names.SemanticKernelVersion,
            HttpHeaderConstant.Values.GetAssemblyVersion(typeof(GoogleAIHttpRequestFactory)));
        return httpRequestMessage;
    }
}
