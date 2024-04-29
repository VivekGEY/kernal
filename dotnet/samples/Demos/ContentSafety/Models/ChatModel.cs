﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;

namespace ContentSafety.Models;

/// <summary>
/// Request model for chat endpoint.
/// </summary>
public class ChatModel
{
    [Required]
    public string Message { get; set; }
}
