﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Experimental.Assistants.Models;

/// <summary>
/// Step in a run on a thread.
/// </summary>
public class ThreadRunStepModel
{
    /// <summary>
    /// Identifier of the run step, which can be referenced in API endpoints.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Always "thread.run.step"
    /// </summary>
    [JsonPropertyName("object")]
#pragma warning disable CA1720 // Identifier contains type name - We don't control the schema
    public string Object { get; set; } = "thread.run.step";
#pragma warning restore CA1720 // Identifier contains type name

    /// <summary>
    /// Unix timestamp (in seconds) for when the run step was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    /// <summary>
    /// The ID of the run to which the run step belongs.
    /// </summary>
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the assistant associated with the run step.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string AssistantId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the thread to which the run and run step belongs.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// The type of run step, which can be either message_creation or tool_calls.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The status of the run step, which can be one of:
    /// in_progress, cancelled, failed, completed, or expired.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Unix timestamp (in seconds) for when the run step was cancelled.
    /// </summary>
    [JsonPropertyName("cancelled_at")]
    public long? CancelledAt { get; set; }

    /// <summary>
    /// Unix timestamp (in seconds) for when the run step completed.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public long? CompletedAt { get; set; }

    /// <summary>
    /// Unix timestamp (in seconds) for when the run step expired.
    /// A step is considered expired if the parent run is expired.
    /// </summary>
    [JsonPropertyName("expired_at")]
    public long? ExpiredAt { get; set; }

    /// <summary>
    /// Unix timestamp (in seconds) for when the run step failed.
    /// </summary>
    [JsonPropertyName("failed_at")]
    public long? FailedAt { get; set; }

    /// <summary>
    /// The last error associated with this run step. Will be null if there are no errors.
    /// </summary>
    [JsonPropertyName("last_error")]
    public string LastError { get; set; } = string.Empty;

    /// <summary>
    /// The details of the run step.
    /// </summary>
    [JsonPropertyName("step_details")]
    public StepDetailsModel StepDetails { get; set; }

    /// <summary>
    /// Details of a run step.
    /// </summary>
    public class StepDetailsModel
    {
        /// <summary>
        /// Type of detail.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Details of the message creation by the run step.
        /// </summary>
        [JsonPropertyName("message_creation")]
        public MessageCreationDetailsModel? MessageCreation { get; set; }

        /// <summary>
        /// Details of tool calls.
        /// </summary>
        [JsonPropertyName("tool_calls")]
        public List<ToolCallsDetailsModel>? ToolCalls { get; set; }
    }

    /// <summary>
    /// Message creation details.
    /// </summary>
    public class MessageCreationDetailsModel
    {
        /// <summary>
        /// ID of the message that was created by this run step.
        /// </summary>
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tool call details.
    /// TODO: This model and all those below it are not quite correct - Fix that
    /// </summary>
    public class ToolCallsDetailsModel
    {
        /// <summary>
        /// ID of the tool call.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The type of tool call.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The definition of the function that was called.
        /// </summary>
        [JsonPropertyName("function")]
        public FunctionDetailsModel Function { get; set; }
    }

    /// <summary>
    /// Function call details.
    /// </summary>
    public class FunctionDetailsModel
    {
        /// <summary>
        /// The name of the function.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The arguments passed to the function.
        /// </summary>
        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// The output of the function.
        /// This will be null if the outputs have not been submitted yet.
        /// </summary>
        public string Output { get; set; } = string.Empty;
    }
}
