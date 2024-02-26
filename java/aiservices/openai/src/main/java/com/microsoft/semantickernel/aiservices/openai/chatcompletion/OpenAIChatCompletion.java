// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.aiservices.openai.chatcompletion;

import com.azure.ai.openai.OpenAIAsyncClient;
import com.azure.ai.openai.models.ChatChoice;
import com.azure.ai.openai.models.ChatCompletions;
import com.azure.ai.openai.models.ChatCompletionsFunctionToolCall;
import com.azure.ai.openai.models.ChatCompletionsFunctionToolDefinition;
import com.azure.ai.openai.models.ChatCompletionsOptions;
import com.azure.ai.openai.models.ChatCompletionsToolCall;
import com.azure.ai.openai.models.ChatCompletionsToolDefinition;
import com.azure.ai.openai.models.ChatRequestAssistantMessage;
import com.azure.ai.openai.models.ChatRequestMessage;
import com.azure.ai.openai.models.ChatRequestSystemMessage;
import com.azure.ai.openai.models.ChatRequestToolMessage;
import com.azure.ai.openai.models.ChatRequestUserMessage;
import com.azure.ai.openai.models.ChatResponseMessage;
import com.azure.core.util.BinaryData;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.microsoft.semantickernel.Kernel;
import com.microsoft.semantickernel.contextvariables.ContextVariable;
import com.microsoft.semantickernel.contextvariables.ContextVariableTypes;
import com.microsoft.semantickernel.exceptions.AIException;
import com.microsoft.semantickernel.exceptions.SKException;
import com.microsoft.semantickernel.hooks.KernelHooks;
import com.microsoft.semantickernel.hooks.PreChatCompletionEvent;
import com.microsoft.semantickernel.orchestration.FunctionResult;
import com.microsoft.semantickernel.orchestration.FunctionResultMetadata;
import com.microsoft.semantickernel.orchestration.InvocationContext;
import com.microsoft.semantickernel.orchestration.PromptExecutionSettings;
import com.microsoft.semantickernel.orchestration.ToolCallBehavior;
import com.microsoft.semantickernel.semanticfunctions.KernelFunction;
import com.microsoft.semantickernel.semanticfunctions.KernelFunctionArguments;
import com.microsoft.semantickernel.services.chatcompletion.AuthorRole;
import com.microsoft.semantickernel.services.chatcompletion.ChatCompletionService;
import com.microsoft.semantickernel.services.chatcompletion.ChatHistory;
import com.microsoft.semantickernel.services.chatcompletion.ChatMessageContent;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.stream.Collectors;
import javax.annotation.Nullable;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import reactor.core.publisher.Flux;
import reactor.core.publisher.Mono;

public class OpenAIChatCompletion implements ChatCompletionService {

    private static final Logger LOGGER = LoggerFactory.getLogger(OpenAIChatCompletion.class);
    private final OpenAIAsyncClient client;

    @Nullable
    private final String serviceId;
    private final String modelId;

    public OpenAIChatCompletion(
        OpenAIAsyncClient client,
        String modelId,
        @Nullable String serviceId) {
        this.serviceId = serviceId;
        this.client = client;
        this.modelId = modelId;
    }

    public static OpenAIChatCompletion.Builder builder() {
        return new OpenAIChatCompletion.Builder();
    }

    @Override
    @Nullable
    public String getServiceId() {
        return serviceId;
    }

    @Override
    public Mono<List<ChatMessageContent<?>>> getChatMessageContentsAsync(
        ChatHistory chatHistory,
        @Nullable Kernel kernel,
        @Nullable InvocationContext invocationContext) {

        List<ChatRequestMessage> chatRequestMessages = getChatRequestMessages(chatHistory);
        return internalChatMessageContentsAsync(
            chatRequestMessages,
            kernel,
            invocationContext);
    }

    @Override
    public Mono<List<ChatMessageContent<?>>> getChatMessageContentsAsync(
        String prompt,
        @Nullable Kernel kernel,
        @Nullable InvocationContext invocationContext) {
        ParsedPrompt parsedPrompt = XMLPromptParser.parse(prompt);

        return internalChatMessageContentsAsync(
            parsedPrompt.getChatRequestMessages(),
            kernel,
            invocationContext);
    }

    private Mono<List<ChatMessageContent<?>>> internalChatMessageContentsAsync(
        List<ChatRequestMessage> messages,
        @Nullable Kernel kernel,
        @Nullable InvocationContext invocationContext) {

        List<OpenAIFunction> functions = new ArrayList<>();
        if (kernel != null) {
            kernel.getPlugins()
                .forEach(plugin -> plugin.getFunctions().forEach((name, function) -> functions
                    .add(OpenAIFunction.build(function.getMetadata(), plugin.getName()))));
        }

        // Create copy to avoid reactor exceptions when updating request messages internally
        return internalChatMessageContentsAsync(
            new ArrayList<>(messages),
            kernel,
            functions,
            invocationContext,
            Math.min(MAXIMUM_INFLIGHT_AUTO_INVOKES,
                invocationContext != null && invocationContext.getToolCallBehavior() != null
                    ? invocationContext.getToolCallBehavior().getMaximumAutoInvokeAttempts()
                    : 0));
    }

    private Mono<List<ChatMessageContent<?>>> internalChatMessageContentsAsync(
        List<ChatRequestMessage> messages,
        @Nullable Kernel kernel,
        List<OpenAIFunction> functions,
        @Nullable InvocationContext invocationContext,
        int autoInvokeAttempts) {

        KernelHooks kernelHooks = invocationContext != null
            && invocationContext.getKernelHooks() != null
                ? invocationContext.getKernelHooks()
                : new KernelHooks();

        ChatCompletionsOptions options = kernelHooks
            .executeHooks(new PreChatCompletionEvent(
                getCompletionsOptions(this, messages,
                    functions, invocationContext, autoInvokeAttempts)))
            .getOptions();

        Mono<List<? extends ChatMessageContent>> result = client
            .getChatCompletions(getModelId(), options)
            .flatMap(completions -> {
                List<ChatResponseMessage> responseMessages = completions
                    .getChoices()
                    .stream()
                    .map(ChatChoice::getMessage)
                    .filter(Objects::nonNull)
                    .collect(Collectors.toList());

                // Just return the result:
                // If we don't want to attempt to invoke any functions
                // Or if we are auto-invoking, but we somehow end up with other than 1 choice even though only 1 was requested
                if (autoInvokeAttempts == 0 || responseMessages.size() != 1) {
                    return getChatMessageContentsAsync(completions);
                }
                // Or if there are no tool calls to be done
                ChatResponseMessage response = responseMessages.get(0);
                List<ChatCompletionsToolCall> toolCalls = response.getToolCalls();
                if (toolCalls == null || toolCalls.isEmpty()) {
                    return getChatMessageContentsAsync(completions);
                }

                ChatRequestAssistantMessage requestMessage = new ChatRequestAssistantMessage(
                    response.getContent());
                requestMessage.setToolCalls(toolCalls);

                // Add the original assistant message to the chat options; this is required for the service
                // to understand the tool call responses
                messages.add(requestMessage);

                return Flux
                    .fromIterable(toolCalls)
                    .reduce(
                        Mono.just(messages),
                        (requestMessages, toolCall) -> {
                            if (toolCall instanceof ChatCompletionsFunctionToolCall) {
                                return requestMessages
                                    .flatMap(msgs -> {
                                        // OpenAI only supports function tool call at the moment
                                        ChatCompletionsFunctionToolCall functionToolCall = (ChatCompletionsFunctionToolCall) toolCall;
                                        if (kernel == null) {
                                            return Mono.<List<ChatRequestMessage>>error(new SKException(
                                                "A tool call was requested, but no kernel was provided to the invocation, this is a unsupported configuration"));
                                        }

                                        return invokeFunctionTool(kernel, functionToolCall)
                                            .map(functionResult -> {
                                                // Add chat request tool message to the chat options
                                                ChatRequestMessage requestToolMessage = new ChatRequestToolMessage(
                                                    functionResult.getResult(),
                                                    functionToolCall.getId());

                                                msgs.add(requestToolMessage);
                                                return msgs;
                                            });
                                    });
                            }
                            return requestMessages;
                        })
                    .flatMap(it -> it)
                    .flatMap(
                        msgs -> internalChatMessageContentsAsync(msgs, kernel, functions,
                            invocationContext, autoInvokeAttempts - 1));
            });

        return result.map(op -> (List<ChatMessageContent<?>>) op);
    }

    @SuppressWarnings("StringSplitter")
    private Mono<FunctionResult<String>> invokeFunctionTool(
        Kernel kernel,
        ChatCompletionsFunctionToolCall toolCall) {
        // Split the full name of a function into plugin and function name
        String name = toolCall.getFunction().getName();
        String[] parts = name.split(OpenAIFunction.getNameSeparator());
        String pluginName = parts.length > 1 ? parts[0] : "";
        String fnName = parts.length > 1 ? parts[1] : parts[0];

        KernelFunction<?> function = kernel.getFunction(pluginName, fnName);

        KernelFunctionArguments arguments = KernelFunctionArguments.builder().build();

        try {
            ObjectMapper mapper = new ObjectMapper();
            JsonNode jsonToolCallArguments = mapper.readTree(toolCall.getFunction().getArguments());

            jsonToolCallArguments.fields().forEachRemaining(
                entry -> arguments.put(entry.getKey(),
                    ContextVariable.of(entry.getValue().asText())));
        } catch (JsonProcessingException e) {
            LOGGER.error("Failed to parse json", e);
            return Mono.empty();
        }

        return function
            .invokeAsync(kernel)
            .withArguments(arguments)
            .withResultType(ContextVariableTypes.getGlobalVariableTypeForClass(
                String.class));
    }

    private Mono<List<ChatMessageContent>> getChatMessageContentsAsync(
        ChatCompletions completions) {
        FunctionResultMetadata completionMetadata = FunctionResultMetadata.build(
            completions.getId(),
            completions.getUsage(),
            completions.getCreatedAt());

        List<ChatResponseMessage> responseMessages = completions
            .getChoices()
            .stream()
            .map(ChatChoice::getMessage)
            .filter(Objects::nonNull)
            .collect(Collectors.toList());

        return Flux.fromIterable(responseMessages)
            .map(response -> new ChatMessageContent(
                AuthorRole.ASSISTANT,
                response.getContent(),
                this.getModelId(),
                null,
                null,
                completionMetadata))
            .collectList();
    }

    private static ChatCompletionsOptions getCompletionsOptions(
        ChatCompletionService chatCompletionService,
        List<ChatRequestMessage> chatRequestMessages,
        @Nullable List<OpenAIFunction> functions,
        @Nullable InvocationContext invocationContext,
        int autoInvokeAttempts) {

        ChatCompletionsOptions options = new ChatCompletionsOptions(chatRequestMessages)
            .setModel(chatCompletionService.getModelId());

        if (invocationContext != null && invocationContext.getToolCallBehavior() != null) {
            configureToolCallBehaviorOptions(options, invocationContext.getToolCallBehavior(),
                functions, autoInvokeAttempts);
        }

        PromptExecutionSettings promptExecutionSettings = invocationContext != null
            ? invocationContext.getPromptExecutionSettings()
            : null;

        if (promptExecutionSettings == null) {
            return options;
        }

        if (promptExecutionSettings.getResultsPerPrompt() < 1
            || promptExecutionSettings.getResultsPerPrompt() > MAX_RESULTS_PER_PROMPT) {
            throw new AIException(AIException.ErrorCodes.INVALID_REQUEST,
                String.format("Results per prompt must be in range between 1 and %d, inclusive.",
                    MAX_RESULTS_PER_PROMPT));
        }

        Map<String, Integer> logit = null;
        if (promptExecutionSettings.getTokenSelectionBiases() != null) {
            logit = promptExecutionSettings
                .getTokenSelectionBiases()
                .entrySet()
                .stream()
                .collect(Collectors.toMap(
                    entry -> entry.getKey().toString(),
                    Map.Entry::getValue));
        }

        options
            .setTemperature(promptExecutionSettings.getTemperature())
            .setTopP(promptExecutionSettings.getTopP())
            .setPresencePenalty(promptExecutionSettings.getPresencePenalty())
            .setFrequencyPenalty(promptExecutionSettings.getFrequencyPenalty())
            .setPresencePenalty(promptExecutionSettings.getPresencePenalty())
            .setMaxTokens(promptExecutionSettings.getMaxTokens())
            .setN(promptExecutionSettings.getResultsPerPrompt())
            // Azure OpenAI WithData API does not allow to send empty array of stop sequences
            // Gives back "Validation error at #/stop/str: Input should be a valid string\nValidation error at #/stop/list[str]: List should have at least 1 item after validation, not 0"
            .setStop(promptExecutionSettings.getStopSequences() == null
                || promptExecutionSettings.getStopSequences().isEmpty() ? null
                    : promptExecutionSettings.getStopSequences())
            .setUser(promptExecutionSettings.getUser())
            .setLogitBias(logit);

        return options;
    }

    private static void configureToolCallBehaviorOptions(
        ChatCompletionsOptions options,
        @Nullable ToolCallBehavior toolCallBehavior,
        @Nullable List<OpenAIFunction> functions,
        int autoInvokeAttempts) {

        if (functions == null || functions.isEmpty()) {
            return;
        }

        if (toolCallBehavior == null || autoInvokeAttempts == 0) {
            // if auto-invoked is not enabled, then we don't need to send any tool definitions
            return;
        }

        // If a specific function is required to be called
        if (toolCallBehavior instanceof ToolCallBehavior.RequiredKernelFunction) {
            KernelFunction<?> toolChoice = ((ToolCallBehavior.RequiredKernelFunction) toolCallBehavior)
                .getRequiredFunction();
            List<ChatCompletionsToolDefinition> toolDefinitions = new ArrayList<>();

            toolDefinitions.add(new ChatCompletionsFunctionToolDefinition(
                OpenAIFunction.toFunctionDefinition(
                    toolChoice.getMetadata(),
                    toolChoice.getPluginName())));

            options.setTools(toolDefinitions);
            try {
                String json = String.format(
                    "{\"type\":\"function\",\"function\":{\"name\":\"%s%s%s\"}}",
                    toolChoice.getPluginName(),
                    OpenAIFunction.getNameSeparator(),
                    toolChoice.getName());
                options.setToolChoice(BinaryData.fromObject(new ObjectMapper().readTree(json)));
            } catch (JsonProcessingException e) {
                throw new RuntimeException(e);
            }
            return;
        }

        // If a set of functions are enabled to be called
        ToolCallBehavior.EnabledKernelFunctions enabledKernelFunctions = (ToolCallBehavior.EnabledKernelFunctions) toolCallBehavior;
        List<ChatCompletionsToolDefinition> toolDefinitions = functions.stream()
            .filter(function -> {
                // check if all kernel functions are enabled
                if (enabledKernelFunctions.isAllKernelFunctionsEnabled()) {
                    return true;
                }
                // otherwise, check for the specific function
                return enabledKernelFunctions.functionEnabled(function.getPluginName(),
                    function.getName());
            })
            .map(OpenAIFunction::getFunctionDefinition)
            .map(ChatCompletionsFunctionToolDefinition::new)
            .collect(Collectors.toList());

        if (toolDefinitions.isEmpty()) {
            return;
        }

        options.setTools(toolDefinitions);
        options.setToolChoice(BinaryData.fromString("auto"));
    }

    private static List<ChatRequestMessage> getChatRequestMessages(ChatHistory chatHistory) {
        List<ChatMessageContent<?>> messages = chatHistory.getMessages();
        if (messages == null || messages.isEmpty()) {
            return new ArrayList<>();
        }
        return messages.stream()
            .map(message -> {
                AuthorRole authorRole = message.getAuthorRole();
                String content = message.getContent();
                return getChatRequestMessage(authorRole, content);
            })
            .collect(Collectors.toList());
    }

    static ChatRequestMessage getChatRequestMessage(
        AuthorRole authorRole,
        @Nullable String content) {

        switch (authorRole) {
            case ASSISTANT:
                return new ChatRequestAssistantMessage(content);
            case SYSTEM:
                return new ChatRequestSystemMessage(content);
            case USER:
                return new ChatRequestUserMessage(content);
            case TOOL:
                return new ChatRequestToolMessage(content, null);
            default:
                LOGGER.debug("Unexpected author role: " + authorRole);
                throw new SKException("Unexpected author role: " + authorRole);
        }

    }

    @Nullable
    @Override
    public String getModelId() {
        return modelId;
    }

    public static class Builder extends ChatCompletionService.Builder {

        @Override
        public OpenAIChatCompletion build() {

            if (this.client == null) {
                throw new AIException(AIException.ErrorCodes.INVALID_REQUEST,
                    "OpenAI client must be provided");
            }

            if (this.modelId == null || modelId.isEmpty()) {
                throw new AIException(AIException.ErrorCodes.INVALID_REQUEST,
                    "OpenAI model id must be provided");
            }

            return new OpenAIChatCompletion(client, modelId, serviceId);
        }
    }
}
