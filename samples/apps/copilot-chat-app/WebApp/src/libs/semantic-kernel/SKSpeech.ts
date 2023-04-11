// Copyright (c) Microsoft. All rights reserved.

import * as speechSdk from 'microsoft-cognitiveservices-speech-sdk';

interface TokenResponse {
    token: string;
    region: string;
}

interface SpeechServiceRequest {
    commandPath: string;
    method?: string;
}

export class SKSpeechService {
    getSpeechRecognizerAsync = async () => {
        if (0) {
            var subscriptionKey = ''; //'YourSubscriptionKey';
            var serviceRegion = 'westus2'; // e.g., "westus"

            const speechConfig = speechSdk.SpeechConfig.fromSubscription(subscriptionKey, serviceRegion);
            speechConfig.speechRecognitionLanguage = 'en-US';
            const audioConfig = speechSdk.AudioConfig.fromDefaultMicrophoneInput();
            return new speechSdk.SpeechRecognizer(speechConfig, audioConfig);
        } else {
            // call AzureSpeechTokenController (in SpeechController.cs) from the backend
            // get a token from that

            const response = await this.invokeAsync();
            const { token, region } = response;
            const speechConfig = speechSdk.SpeechConfig.fromAuthorizationToken(token, region);
            speechConfig.speechRecognitionLanguage = 'en-US';
            const audioConfig = speechSdk.AudioConfig.fromDefaultMicrophoneInput();
            return new speechSdk.SpeechRecognizer(speechConfig, audioConfig);
        }
    };

    private invokeAsync = async (): Promise<TokenResponse> => {
        const result = await this.getAzureSpeechTokenAsync<TokenResponse>({
            commandPath: `token/speech`,
            method: 'GET',
        });
        return result;
    };

    private readonly getAzureSpeechTokenAsync = async <TokenResponse>(
        request: SpeechServiceRequest,
    ): Promise<TokenResponse> => {
        const { commandPath, method } = request;

        try {
            const requestUrl = new URL(commandPath);
            const response = await fetch(requestUrl, {
                method: method ?? 'GET',
                headers: { 'Content-Type': 'application/json' },
            });

            if (!response.ok) {
                throw Object.assign(new Error(response.statusText + ' => ' + (await response.text())));
            }

            return (await response.json()) as TokenResponse;
        } catch (e) {
            var additional_error_msg = '';
            if (e instanceof TypeError) {
                // fetch() will reject with a TypeError when a network error is encountered.
                additional_error_msg =
                    '\n\nPlease check that your backend is running and that it is accessible by the app';
            }
            throw Object.assign(new Error(e + additional_error_msg));
        }
    };
}