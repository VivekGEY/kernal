﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace Microsoft.SemanticKernel.Connectors.AI.Oobabooga.Completion.TextCompletion;

public class OobaboogaTextCompletion : OobaboogaCompletionBase<string, CompleteRequestSettings, OobaboogaCompletionParameters, OobaboogaCompletionRequest, TextCompletionResponse, TextCompletionResult, TextCompletionStreamingResult>, ITextCompletion
{
    private const string BlockingUriPath = "/api/v1/generate";
    private const string StreamingUriPath = "/api/v1/stream";

    /// <summary>
    /// Initializes a new instance of the <see cref="OobaboogaTextCompletion"/> class.
    /// </summary>
    /// <param name="endpoint">The service API endpoint to which requests should be sent.</param>
    /// <param name="blockingPort">The port used for handling blocking requests. Default value is 5000</param>
    /// <param name="streamingPort">The port used for handling streaming requests. Default value is 5005</param>
    /// <param name="completionRequestSettings">An instance of <see cref="OobaboogaCompletionSettings"/>, which are text completion settings specific to Oobabooga api</param>
    /// <param name="concurrentSemaphore">You can optionally set a hard limit on the max number of concurrent calls to the either of the completion methods by providing a <see cref="SemaphoreSlim"/>. Calls in excess will wait for existing consumers to release the semaphore</param>
    /// <param name="httpClient">Optional. The HTTP client used for making blocking API requests. If not specified, a default client will be used.</param>
    /// <param name="useWebSocketsPooling">If true, websocket clients will be recycled in a reusable pool as long as concurrent calls are detected</param>
    /// <param name="webSocketsCleanUpCancellationToken">if websocket pooling is enabled, you can provide an optional CancellationToken to properly dispose of the clean up tasks when disposing of the connector</param>
    /// <param name="keepAliveWebSocketsDuration">When pooling is enabled, pooled websockets are flushed on a regular basis when no more connections are made. This is the time to keep them in pool before flushing</param>
    /// <param name="webSocketFactory">The WebSocket factory used for making streaming API requests. Note that only when pooling is enabled will websocket be recycled and reused for the specified duration. Otherwise, a new websocket is created for each call and closed and disposed afterwards, to prevent data corruption from concurrent calls.</param>
    /// <param name="logger">Application logger</param>
    public OobaboogaTextCompletion(Uri endpoint,
        int blockingPort = 5000,
        int streamingPort = 5005,
        OobaboogaTextCompletionSettings? completionRequestSettings = null,
        SemaphoreSlim? concurrentSemaphore = null,
        HttpClient? httpClient = null,
        bool useWebSocketsPooling = true,
        CancellationToken? webSocketsCleanUpCancellationToken = default,
        int keepAliveWebSocketsDuration = 100,
        Func<ClientWebSocket>? webSocketFactory = null,
        ILogger? logger = null) : base(endpoint, BlockingUriPath, StreamingUriPath, blockingPort, streamingPort, completionRequestSettings, concurrentSemaphore, httpClient, useWebSocketsPooling, webSocketsCleanUpCancellationToken, keepAliveWebSocketsDuration, webSocketFactory, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ITextResult>> GetCompletionsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        CancellationToken cancellationToken = default)
    {
        this.LogActionDetails();
        return await this.GetCompletionsBaseAsync(text, requestSettings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ITextStreamingResult> GetStreamingCompletionsAsync(
        string text,
        CompleteRequestSettings requestSettings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chatCompletionStreamingResult in this.GetStreamingCompletionsBaseAsync(text, requestSettings, cancellationToken))
        {
            yield return chatCompletionStreamingResult;
        }
    }

    /// <summary>
    /// Creates an Oobabooga request, mapping CompleteRequestSettings fields to their Oobabooga API counter parts
    /// </summary>
    /// <param name="input">The text to complete.</param>
    /// <param name="requestSettings">The request settings.</param>
    /// <returns>An Oobabooga TextCompletionRequest object with the text and completion parameters.</returns>
    protected override OobaboogaCompletionRequest CreateCompletionRequest(string input, CompleteRequestSettings? requestSettings)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentNullException(nameof(input));
        }

        requestSettings ??= new CompleteRequestSettings();

        // Prepare the request using the provided parameters.
        var toReturn = OobaboogaCompletionRequest.Create(input, this.OobaboogaSettings, requestSettings);
        return toReturn;
    }

    protected override IReadOnlyList<TextCompletionResult> GetCompletionResults(TextCompletionResponse completionResponse)
    {
        return completionResponse.Results.Select(completionText => new TextCompletionResult(completionText)).ToList();
    }

    protected override CompletionStreamingResponseBase? GetResponseObject(string messageText)
    {
        return JsonSerializer.Deserialize<TextCompletionStreamingResponse>(messageText);
    }
}
