﻿using Microsoft.AspNetCore.Mvc;

namespace SemanticKernel.Service.Plugins;

/// /// <summary>
/// Represents the authentication headers for imported Open API Plugin Skills.
/// </summary>
public class PluginAuthHeaders
{
    /// <summary>
    /// Gets or sets the MS Graph authentication header value.
    /// </summary>
    [FromHeader(Name = "x-sk-copilot-graph-auth")]
    public string? GraphAuthentication { get; set; }

    /// <summary>
    /// Gets or sets the Jira authentication header value.
    /// </summary>
    [FromHeader(Name = "x-sk-copilot-jira-auth")]
    public string? JiraAuthentication { get; set; }

    /// <summary>
    /// Gets or sets the GitHub authentication header value.
    /// </summary>
    [FromHeader(Name = "x-sk-copilot-github-auth")]
    public string? GithubAuthentication { get; set; }
}
