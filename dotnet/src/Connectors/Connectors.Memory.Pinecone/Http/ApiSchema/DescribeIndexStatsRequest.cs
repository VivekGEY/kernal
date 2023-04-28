// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Http.ApiSchema;

/// <summary>
/// DescribeIndexStatsRequest
/// See https://docs.pinecone.io/reference/describe_index_stats_post
/// </summary>
internal sealed class DescribeIndexStatsRequest
{
    /// <summary>
    /// If this parameter is present, the operation only affects vectors that satisfy the filter. See https://www.pinecone.io/docs/metadata-filtering/.
    /// </summary>
    [JsonPropertyName("filter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Filter { get; set; }

    public static DescribeIndexStatsRequest GetIndexStats()
    {
        return new DescribeIndexStatsRequest();
    }

    public DescribeIndexStatsRequest WithFilter(Dictionary<string, object>? filter)
    {
        this.Filter = filter;
        return this;
    }

    public HttpRequestMessage Build()
    {
        return this.Filter == null
            ? HttpRequest.CreatePostRequest("/describe_index_stats")
            : HttpRequest
                .CreatePostRequest("/describe_index_stats", this);
    }

    #region private ================================================================================

    private DescribeIndexStatsRequest()
    {
    }

    #endregion

}
