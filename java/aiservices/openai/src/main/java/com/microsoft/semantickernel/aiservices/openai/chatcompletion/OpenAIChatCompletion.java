package com.microsoft.semantickernel.aiservices.openai.chatcompletion;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.stream.Collector;
import java.util.stream.Collectors;

import javax.annotation.Nullable;
import javax.xml.namespace.QName;
import javax.xml.stream.XMLEventReader;
import javax.xml.stream.XMLInputFactory;
import javax.xml.stream.XMLStreamException;
import javax.xml.stream.events.Attribute;
import javax.xml.stream.events.StartElement;
import javax.xml.stream.events.XMLEvent;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.azure.ai.openai.OpenAIAsyncClient;
import com.azure.ai.openai.models.ChatChoice;
import com.azure.ai.openai.models.ChatCompletions;
import com.azure.ai.openai.models.ChatCompletionsFunctionToolCall;
import com.azure.ai.openai.models.ChatCompletionsFunctionToolDefinition;
import com.azure.ai.openai.models.ChatCompletionsOptions;
import com.azure.ai.openai.models.ChatCompletionsToolCall;
import com.azure.ai.openai.models.ChatRequestAssistantMessage;
import com.azure.ai.openai.models.ChatRequestMessage;
import com.azure.ai.openai.models.ChatRequestSystemMessage;
import com.azure.ai.openai.models.ChatRequestToolMessage;
import com.azure.ai.openai.models.ChatRequestUserMessage;
import com.azure.ai.openai.models.ChatResponseMessage;
import com.azure.ai.openai.models.ChatRole;
import com.azure.ai.openai.models.FunctionCall;
import com.azure.ai.openai.models.FunctionDefinition;
import com.azure.core.util.BinaryData;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.microsoft.semantickernel.Kernel;
import com.microsoft.semantickernel.chatcompletion.AuthorRole;
import com.microsoft.semantickernel.chatcompletion.ChatCompletionService;
import com.microsoft.semantickernel.chatcompletion.ChatHistory;
import com.microsoft.semantickernel.chatcompletion.ChatMessageContent;
import com.microsoft.semantickernel.chatcompletion.StreamingChatMessageContent;
import com.microsoft.semantickernel.orchestration.KernelFunction;
import com.microsoft.semantickernel.orchestration.PromptExecutionSettings;
import com.microsoft.semantickernel.orchestration.contextvariables.ContextVariable;
import com.microsoft.semantickernel.orchestration.contextvariables.ContextVariableType;
import com.microsoft.semantickernel.orchestration.contextvariables.ContextVariableTypes;
import com.microsoft.semantickernel.orchestration.contextvariables.KernelArguments;

import reactor.core.publisher.Flux;
import reactor.core.publisher.Mono;

public class OpenAIChatCompletion implements ChatCompletionService {

    private static final Logger LOGGER = LoggerFactory.getLogger(OpenAIChatCompletion.class);

    private final OpenAIAsyncClient client;
    private final Map<String, ContextVariable<?>> attributes;

    public OpenAIChatCompletion(OpenAIAsyncClient client, String modelId) {
        this.client = client;
        this.attributes = new HashMap<>();
        attributes.put(MODEL_ID_KEY, ContextVariable.of(modelId));
    }

    @Override
    public Map<String, ContextVariable<?>> getAttributes() {
        return attributes;
    }

    @Override
    public Mono<List<ChatMessageContent>> getChatMessageContentsAsync(ChatHistory chatHistory,
            @Nullable PromptExecutionSettings promptExecutionSettings, @Nullable Kernel kernel) {

        List<ChatRequestMessage> chatRequestMessages = getChatRequestMessages(chatHistory);
        List<FunctionDefinition> functions = Collections.emptyList();
        return internalChatMessageContentsAsync(chatRequestMessages, functions, promptExecutionSettings, kernel);

    }

    @Override
    public Flux<StreamingChatMessageContent> getStreamingChatMessageContentsAsync(ChatHistory chatHistory,
            PromptExecutionSettings promptExecutionSettings, Kernel kernel)
    {
        List<ChatRequestMessage> chatRequestMessages = getChatRequestMessages(chatHistory);
        List<FunctionDefinition> functions = Collections.emptyList();
        return internalStreamingChatMessageContentsAsync(chatRequestMessages, functions, promptExecutionSettings, kernel);

    }

    @Override
    public Mono<List<ChatMessageContent>> getChatMessageContentsAsync(String prompt,
            PromptExecutionSettings promptExecutionSettings, Kernel kernel) {
        List<ChatRequestMessage> chatRequestMessages = getChatRequestMessages(prompt);
        List<FunctionDefinition> functions = getFunctionDefinitions(prompt);
        return internalChatMessageContentsAsync(chatRequestMessages, functions, promptExecutionSettings, kernel);
    }

    @Override
    public Flux<StreamingChatMessageContent> getStreamingChatMessageContentsAsync(String prompt,
            PromptExecutionSettings promptExecutionSettings, Kernel kernel) {
        List<ChatRequestMessage> chatRequestMessages = getChatRequestMessages(prompt);
        List<FunctionDefinition> functions = getFunctionDefinitions(prompt);
        return internalStreamingChatMessageContentsAsync(chatRequestMessages, functions, promptExecutionSettings, kernel);
    }

    private Flux<StreamingChatMessageContent> internalStreamingChatMessageContentsAsync(
        List<ChatRequestMessage> chatRequestMessages,
        List<FunctionDefinition> functions,
        PromptExecutionSettings promptExecutionSettings,
        Kernel kernel)
    {
        ChatCompletionsOptions options = getCompletionsOptions(this, chatRequestMessages, functions,  promptExecutionSettings);
        Flux<List<StreamingChatMessageContent>> results = 
            internalStreamingChatMessageContentsAsync(options, kernel).buffer();
        return results.flatMap(list -> {
                boolean makeSecondCall = false;
                for (StreamingChatMessageContent messageContent : list) {
                    if (messageContent.getRole() == AuthorRole.TOOL) {
                        makeSecondCall = true;
                        String content = messageContent.getContent();
                        String id = messageContent.getModelId();
                        ChatRequestToolMessage toolMessage = new ChatRequestToolMessage(content, id);
                        chatRequestMessages.add(toolMessage);
                    }
                }
                if (makeSecondCall) {
                    return internalStreamingChatMessageContentsAsync(options, kernel); 
                }
                return Flux.fromIterable(list); 
            });
    }

    private Flux<StreamingChatMessageContent> internalStreamingChatMessageContentsAsync(ChatCompletionsOptions options, Kernel kernel) {
        return client
            .getChatCompletionsStream(getModelId(), options)
            .mapNotNull(ChatCompletions::getChoices)
            .filter(choices -> choices != null && !choices.isEmpty())
            .reduceWith(ChatResponseCollector::new, (accumulator, choices) -> accumulateResponsesFromStream(accumulator, choices))
            .map(ChatResponseCollector::toStreamingChatMessageContent)
            .map(streamingChatMessageContent -> {
                if (streamingChatMessageContent.getRole() == AuthorRole.TOOL) {
                    return invokeTool(kernel, streamingChatMessageContent.getContent()).block();
                }
                return streamingChatMessageContent;
            })
            .flux();
    }

    private Mono<List<ChatMessageContent>> internalChatMessageContentsAsync(
        List<ChatRequestMessage> chatRequestMessages,
        List<FunctionDefinition> functions,
        PromptExecutionSettings promptExecutionSettings,
        Kernel kernel)
    {
        ChatCompletionsOptions options = getCompletionsOptions(this, chatRequestMessages, functions,  promptExecutionSettings);
        Mono<List<ChatMessageContent>> results = 
            internalChatMessageContentsAsync(options, kernel);

        return results.flatMap(list -> {
                boolean makeSecondCall = false;
                for (ChatMessageContent messageContent : list) {
                    if (messageContent.getAuthorRole() == AuthorRole.TOOL) {
                        makeSecondCall = true;
                        String content = messageContent.getContent();
                        String id = messageContent.getModelId();
                        ChatRequestToolMessage toolMessage = new ChatRequestToolMessage(content, id);
                        chatRequestMessages.add(toolMessage);
                    }
                }
                if (makeSecondCall) {
                    return internalChatMessageContentsAsync(options, kernel); 
                }
                return Mono.just(list); 
            });
    }

    private Mono<List<ChatMessageContent>> internalChatMessageContentsAsync(ChatCompletionsOptions options, Kernel kernel) {

        return client
            .getChatCompletions(getModelId(), options)
            .mapNotNull(ChatCompletions::getChoices)
            .filter(choices -> choices != null && !choices.isEmpty())
            .map(this::accumulateResponses)
            .map(responses -> 
                responses.stream()
                    .map(ChatResponseCollector::toChatMessageContent)
                    .collect(Collectors.toList())
            );
            
    }

    // streaming case
    private ChatResponseCollector accumulateResponsesFromStream(ChatResponseCollector collector, List<ChatChoice> choices) {
        choices.stream()
            .map(choice -> choice.getDelta())
            .filter(chatResponseMessage -> chatResponseMessage != null)
            .forEach(chatResponseMessage -> accumulateResponse(collector, chatResponseMessage)); 
        return collector;
    }

    // non-streaming case
    private List<ChatResponseCollector> accumulateResponses(List<ChatChoice> choices) {
        List<ChatResponseCollector> collectors = new ArrayList<>();
        choices.stream()
            .map(choice -> choice.getDelta())
            .filter(chatResponseMessage -> chatResponseMessage != null)
            .map(this::accumulateResponse)
            .filter(chatResponseMessage -> chatResponseMessage != null)
            .forEach(collectors::add); 
        return collectors;
    }

    private ChatResponseCollector accumulateResponse(ChatResponseMessage response) {
        return accumulateResponse(null, response);
    }

    private ChatResponseCollector accumulateResponse(ChatResponseCollector collector, ChatResponseMessage response) {
        // collector is null for the non-streaming case and not null for the streaming case
        if (response.getContent() != null) {
            if (collector == null) collector = new ChatResponseCollector();
            collector.append(AuthorRole.ASSISTANT, response.getContent());
        } else if (response.getToolCalls() != null) {
            List<ChatCompletionsToolCall> toolCalls = response.getToolCalls();
            // TODO: This assumes one tool call per response, which is _definitely_ a bad assumption.
            for( ChatCompletionsToolCall toolCall : toolCalls) {
                if (collector == null) collector = new ChatResponseCollector();
                if (toolCall instanceof ChatCompletionsFunctionToolCall) {
                    collector.append(AuthorRole.TOOL,(ChatCompletionsFunctionToolCall)toolCall);
                }
            }
        }
        return collector;
    }

    /*
     * Given a json string, invoke the tool specified in the json string.
     * At this time, the only tool we have is 'function'. 
     * The json string should be of the form:
     * {"type":"function", "function": {"name":"search-search", "parameters": {"query":"Banksy"}}}
     * where 'name' is <plugin name '-' function name>.
     */
    private Mono<StreamingChatMessageContent> invokeTool(Kernel kernel, String json) {
        try {
            ObjectMapper mapper = new ObjectMapper();
            JsonNode jsonNode = mapper.readTree(json);
            String id = jsonNode.get("id").asText("");
            jsonNode = jsonNode.get("function");
            if (jsonNode != null) {
                // function is the only tool we have right now. 
                Mono<ContextVariable<String>> result = invokeFunction(kernel, jsonNode);
                if (result != null) {
                    return result.map(contextVariable -> {
                        String content = contextVariable.getValue();
                        return new StreamingChatMessageContent(AuthorRole.TOOL, content).setModelId(id);
                    });
                }
            }
        } catch (JsonProcessingException e) {
            LOGGER.error("Failed to parse json", e);
        }
        return Mono.empty();
    }

    /*
     * The jsonNode should represent: {"name":"search-search", "parameters": {"query":"Banksy"}}}
     */
    private Mono<ContextVariable<String>> invokeFunction(Kernel kernel, JsonNode jsonNode) {
        String name = jsonNode.get("name").asText();
        String[] parts = name.split("-");
        String pluginName = parts.length > 0 ? parts[0] : "";
        String fnName = parts.length > 1 ? parts[1] : "";
        JsonNode parameters = jsonNode.get("parameters");
        KernelFunction kernelFunction = kernel.getPlugins().getFunction(pluginName, fnName);
        if (kernelFunction == null) return Mono.empty();
        
        KernelArguments arguments = null;
        if (parameters != null) {
            Map<String, ContextVariable<?>> variables = new HashMap<>();
            parameters.fields().forEachRemaining(entry -> {
                String paramName = entry.getKey();
                String paramValue = entry.getValue().asText();
                ContextVariable<?> contextVariable = ContextVariable.of(paramValue);                
                variables.put(paramName, contextVariable);
            });
            arguments = KernelArguments.builder().withVariables(variables).build();
        }
        ContextVariableType<String> variableType = ContextVariableTypes.getDefaultVariableTypeForClass(String.class);
        return kernelFunction.invokeAsync(kernel, arguments, variableType);
    }

    private static ChatCompletionsOptions getCompletionsOptions(
        ChatCompletionService chatCompletionService,
        List<ChatRequestMessage> chatRequestMessages,
        List<FunctionDefinition> functions,
        PromptExecutionSettings promptExecutionSettings)
    {
        ChatCompletionsOptions options = new ChatCompletionsOptions(chatRequestMessages)
            .setModel(chatCompletionService.getModelId());

        if (functions != null && !functions.isEmpty()) {
            // options.setFunctions(functions);
            options.setTools(
                functions.stream()
                    .map(ChatCompletionsFunctionToolDefinition::new)
                    .collect(Collectors.toList())
            );
        }

        if (promptExecutionSettings == null) {
            return options;
        }

        options
            .setTemperature(promptExecutionSettings.getTemperature())
            .setTopP(promptExecutionSettings.getTopP())
            .setPresencePenalty(promptExecutionSettings.getPresencePenalty())
            .setFrequencyPenalty(promptExecutionSettings.getFrequencyPenalty())
            .setPresencePenalty(promptExecutionSettings.getPresencePenalty())
            .setMaxTokens(promptExecutionSettings.getMaxTokens())
            // Azure OpenAI WithData API does not allow to send empty array of stop sequences
            // Gives back "Validation error at #/stop/str: Input should be a valid string\nValidation error at #/stop/list[str]: List should have at least 1 item after validation, not 0"
            .setStop(promptExecutionSettings.getStopSequences() == null || promptExecutionSettings.getStopSequences().isEmpty() ? null : promptExecutionSettings.getStopSequences())
            .setUser(promptExecutionSettings.getUser())
            .setLogitBias(new HashMap<>());

        return options;
    }

    private static List<ChatRequestMessage> getChatRequestMessages(ChatHistory chatHistory)
    {
        List<ChatMessageContent> messages = chatHistory.getMessages();
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

    private static List<ChatRequestMessage> getChatRequestMessages(String prompt)
    {
        // TODO: XML parsing should be done as a chain of XMLEvent handlers.
        // If one handler does not recognize the element, it should pass it to the next handler.
        // In this way, we can avoid parsing the whole prompt twice and easily extend the parsing logic.
        List<ChatRequestMessage> messages = new ArrayList<>();
        try (InputStream is = new ByteArrayInputStream(prompt.getBytes())) {
            XMLInputFactory factory = XMLInputFactory.newFactory();
            XMLEventReader reader = factory.createXMLEventReader(is);
            while(reader.hasNext()) {
                XMLEvent event = reader.nextEvent();
                if (event.isStartElement()) {
                    String name = getElementName(event);
                    if (name.equals("message")) {
                        String role = getAttributeValue(event, "role");
                        String content = reader.getElementText();
                        messages.add(getChatRequestMessage(role, content));
                    }
                }
            }
        } catch (IOException | XMLStreamException | IllegalArgumentException e) {
            LOGGER.error("Error parsing prompt", e);
        }
        return messages;
    }

    private static List<FunctionDefinition> getFunctionDefinitions(String prompt)
    {
        // TODO: XML parsing should be done as a chain of XMLEvent handlers. See previous remark.
        // <function pluginName=\"%s\" name=\"%s\"  description=\"%s\">
        //      <parameter name=\"%s\" description=\"%s\" defaultValue=\"%s\" isRequired=\"%s\" type=\"%s\"/>...
        // </function>
        List<FunctionDefinition> functionDefinitions = new ArrayList<>();
        try (InputStream is = new ByteArrayInputStream(prompt.getBytes())) {
            XMLInputFactory factory = XMLInputFactory.newFactory();
            XMLEventReader reader = factory.createXMLEventReader(is);
            FunctionDefinition functionDefinition = null;
            Map<String, String> parameters = new HashMap<>();
            List<String> requiredParmeters = new ArrayList<>();
            while(reader.hasNext()) {
                XMLEvent event = reader.nextEvent();
                if (event.isStartElement()) {
                    String elementName = getElementName(event);
                    if (elementName.equals("function")) {
                        assert functionDefinition == null;
                        assert parameters.isEmpty();
                        assert requiredParmeters.isEmpty();
                        String pluginName = getAttributeValue(event, "pluginName");
                        String name = getAttributeValue(event, "name");
                        String description = getAttributeValue(event, "description");
                        // name has to match '^[a-zA-Z0-9_-]{1,64}$'
                        functionDefinition = new FunctionDefinition(pluginName + "-" +name)
                            .setDescription(description);
                    } else if (elementName.equals("parameter")) {
                        String name = getAttributeValue(event, "name");
                        String type = getAttributeValue(event, "type").toLowerCase(Locale.ROOT);
                        String description = getAttributeValue(event, "description");
                        parameters.put(name, String.format("{\"type\": \"%s\", \"description\": \"%s\"}", "string", description));

                        String isRequired = getAttributeValue(event, "isRequired");
                        if (Boolean.parseBoolean(isRequired)) {
                            requiredParmeters.add(name);
                        }
                    }
                } else if (event.isEndElement()) {
                    String elementName = getElementName(event);
                    if (elementName.equals("function")) {
                        // Example JSON Schema:
                        // {
                        //    "type": "function",
                        //    "function": {
                        //        "name": "get_current_weather",
                        //        "description": "Get the current weather in a given location",
                        //        "parameters": {
                        //            "type": "object",
                        //            "properties": {
                        //                "location": {
                        //                    "type": "string",
                        //                    "description": "The city and state, e.g. San Francisco, CA",
                        //                },
                        //               "unit": {"type": "string", "enum": ["celsius", "fahrenheit"]},
                        //            },
                        //            "required": ["location"],
                        //        },
                        //    },
                        //}
                        assert functionDefinition != null;
                        if (!parameters.isEmpty()) {
                            StringBuilder sb = new StringBuilder("{\"type\": \"object\", \"properties\": {");
                            parameters.forEach((name, value) -> {
                                // make "param": {"type": "string", "description": "desc"},
                                sb.append(String.format("\"%s\": %s,", name, value));
                            });
                            // strip off trailing comma and close the properties object
                            sb.replace(sb.length() - 1, sb.length(), "}");
                            if (!requiredParmeters.isEmpty()) {
                                sb.append(", \"required\": [");
                                requiredParmeters.forEach(name -> {
                                    sb.append(String.format("\"%s\",", name));
                                });
                                // strip off trailing comma and close the required array
                                sb.replace(sb.length() - 1, sb.length(), "]");
                            }
                            // close the object
                            sb.append("}");
                            ObjectMapper objectMapper = new ObjectMapper();
                            JsonNode jsonNode = objectMapper.readTree(sb.toString());
                            BinaryData binaryData = BinaryData.fromObject(jsonNode);
                            functionDefinition.setParameters(binaryData);
                        }
                        functionDefinitions.add(functionDefinition);
                        functionDefinition = null;
                        parameters.clear();
                        requiredParmeters.clear();
                    }
                }
            }
        } catch (IOException | XMLStreamException | IllegalArgumentException e) {
            LOGGER.error("Error parsing prompt", e);
        }
        return functionDefinitions;
    }

    private static String getElementName(XMLEvent xmlEvent) {
        if (xmlEvent.isStartElement()) {
            return xmlEvent.asStartElement().getName().getLocalPart();
        } else if (xmlEvent.isEndElement()) {
            return xmlEvent.asEndElement().getName().getLocalPart();
        }
        // TODO: programmer's error - log at debug
        return "";
    }

    private static String getAttributeValue(XMLEvent xmlEvent, String attributeName)
    {
        if (xmlEvent.isStartElement()) {
            StartElement element = xmlEvent.asStartElement();
            Attribute attribute = element.getAttributeByName(QName.valueOf(attributeName));
            return attribute != null ? attribute.getValue() : "";
        }
        // TODO: programmer's error - log at debug
        return "";
    }
   
    private static ChatRequestMessage getChatRequestMessage(
        String role,
        String content)
    {
        try {
            AuthorRole authorRole = AuthorRole.valueOf(role.toUpperCase());
            return getChatRequestMessage(authorRole, content);
        } catch (IllegalArgumentException e) {
            LOGGER.debug("Unknown author role: " + role);
            return null;
        }
    }

    private static ChatRequestMessage getChatRequestMessage(
        AuthorRole authorRole,
        String content) {

        switch(authorRole) {
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
                return null;
        }
    
    }

    private static List<ChatMessageContent> toChatMessageContents(ChatCompletions chatCompletions) {

        if (chatCompletions == null || chatCompletions.getChoices() == null || chatCompletions.getChoices().isEmpty()) {
            return new ArrayList<>();
        }

        return chatCompletions.getChoices().stream()
            .map(ChatChoice::getMessage)
            .map(OpenAIChatCompletion::toChatMessageContent)
            .collect(Collectors.toList());
    }

    private static ChatMessageContent toChatMessageContent(ChatResponseMessage message) {
        return new ChatMessageContent(toAuthorRole(message.getRole()), message.getContent());
    }

    private static AuthorRole toAuthorRole(ChatRole chatRole) {
        if (chatRole == null) {
            return null;
        }
        if(chatRole == ChatRole.ASSISTANT) {
            return AuthorRole.ASSISTANT;
        }
        if(chatRole == ChatRole.SYSTEM) {
            return AuthorRole.SYSTEM;
        }
        if(chatRole == ChatRole.USER) {
            return AuthorRole.USER;
        }
        if(chatRole == ChatRole.TOOL) {
            return AuthorRole.TOOL;
        }
        throw new IllegalArgumentException("Unknown chat role: " + chatRole);
    }

    private interface ContentBuffer<T> {
        void append(T content);
        StreamingChatMessageContent toStreamingChatMessageContent();
        ChatMessageContent toChatMessageContent();
    }

    private static class AssistantContentBuffer implements ContentBuffer<String> {
        private final StringBuilder sb = new StringBuilder();

        @Override
        public void append(String content) {
            sb.append(content);
        }

        @Override
        public StreamingChatMessageContent toStreamingChatMessageContent() {
            return new StreamingChatMessageContent(AuthorRole.ASSISTANT, sb.toString());
        }

        @Override
        public ChatMessageContent toChatMessageContent() {
            return new ChatMessageContent(AuthorRole.ASSISTANT, sb.toString());
        }
    }

    private static class ToolContentBuffer implements ContentBuffer<ChatCompletionsFunctionToolCall> {

        private String id = null;
        private String name = null;
        private List<String> arguments = new ArrayList<>();

        @Override
        public void append(ChatCompletionsFunctionToolCall toolCall) {
            FunctionCall function = toolCall.getFunction();
            String toolCallId = toolCall.getId();
            String fnName = function.getName();
            String fnArguments = function.getArguments();
            if (this.id == null && toolCallId != null && !toolCallId.isEmpty()) {
                this.id = toolCallId;
            } 
            if (this.name == null && fnName != null && !fnName.isEmpty()) {
                this.name = fnName;
            } 
            if (fnArguments != null && !fnArguments.isEmpty()) {
                this.arguments.add(fnArguments);
            }
        }

        @Override
        public StreamingChatMessageContent toStreamingChatMessageContent() {
            return new StreamingChatMessageContent(AuthorRole.TOOL, toJsonString());
        }

        @Override
        public ChatMessageContent toChatMessageContent() {
            return new ChatMessageContent(AuthorRole.TOOL, toJsonString());
        }

        private String toJsonString() {
            StringBuilder sb = new StringBuilder(
            String.format("{\"type\":\"function\", \"id\":\"%s\", \"function\": ", id));
            sb.append(String.format("{\"name\":\"%s\", \"parameters\": ", name));
            // when concatentated, args should be valid json
            for(String argument : arguments) {
                sb.append(argument);
            }
            // close off function, and type
            sb.append("}}");
            assert isBalanced(sb.toString());
            return sb.toString();
        }

        // used to check that the json string is balanced
        private boolean isBalanced(String str) {
            int openParens = 0;
            int closeParens = 0;
            boolean inString = false;
            for (int i = 0; i < str.length(); i++) {
                char c = str.charAt(i);
                if (!inString && c == '(') {
                    openParens++;
                } else if (!inString && c == ')') {
                    closeParens++;
                } else if (c == '"') {
                    inString = !inString;
                }
            }
            return openParens == closeParens;
        }
    }

    // For streaming, 
    private static class ChatResponseCollector {

        private final Map<AuthorRole,ContentBuffer<?>> roleToContent = new HashMap<>();

        private ContentBuffer<?> collectorFor(AuthorRole role) {
            return roleToContent.computeIfAbsent(role, k -> {
                if (k == AuthorRole.TOOL) return new ToolContentBuffer();
                return new AssistantContentBuffer();
            });
        }

        @SuppressWarnings("unchecked")
        private <T> void append(AuthorRole role, T content) {
            ContentBuffer<T> contentBuffer = (ContentBuffer<T>)collectorFor(role);
            contentBuffer.append(content);
        }

        private StreamingChatMessageContent toStreamingChatMessageContent() {
            assert roleToContent.size() == 1;
            ContentBuffer<?> contentBuffer = roleToContent.values().iterator().next();
            return contentBuffer.toStreamingChatMessageContent();
        }

        private ChatMessageContent toChatMessageContent() {
            assert roleToContent.size() == 1;
            ContentBuffer<?> contentBuffer = roleToContent.values().iterator().next();
            return contentBuffer.toChatMessageContent();
        }
    }


    public static class Builder implements ChatCompletionService.Builder {
        private OpenAIAsyncClient client;
        private String modelId;

        @Override
        public OpenAIChatCompletion build() {
            return new OpenAIChatCompletion(client, modelId);
        }

        @Override
        public Builder withModelId(String modelId) {
            this.modelId = modelId;
            return this;
        }

        @Override
        public Builder withOpenAIAsyncClient(OpenAIAsyncClient openAIClient) {
            this.client = openAIClient;
            return this;
        }
    }
}
