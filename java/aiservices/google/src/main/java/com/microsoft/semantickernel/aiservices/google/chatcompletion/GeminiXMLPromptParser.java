package com.microsoft.semantickernel.aiservices.google.chatcompletion;

import com.azure.core.util.BinaryData;
import com.google.cloud.vertexai.api.FunctionDeclaration;
import com.google.cloud.vertexai.api.Schema;
import com.microsoft.semantickernel.services.chatcompletion.AuthorRole;
import com.microsoft.semantickernel.services.chatcompletion.ChatHistory;
import com.microsoft.semantickernel.services.chatcompletion.ChatMessageContent;
import com.microsoft.semantickernel.services.chatcompletion.ChatPromptParseVisitor;
import com.microsoft.semantickernel.services.chatcompletion.ChatXMLPromptParser;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import javax.annotation.Nullable;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class GeminiXMLPromptParser {
    private static final Logger LOGGER = LoggerFactory.getLogger(GeminiXMLPromptParser.class);

    public static class GeminiParsedPrompt {
        private final ChatHistory chatHistory;
        private final List<FunctionDeclaration> functions;

        protected GeminiParsedPrompt(
                ChatHistory parsedChatHistory,
                @Nullable List<FunctionDeclaration> parsedFunctions
        ) {
            this.chatHistory = parsedChatHistory;
            if (parsedFunctions == null) {
                parsedFunctions = new ArrayList<>();
            }
            this.functions = parsedFunctions;
        }

        public ChatHistory getChatHistory() {
            return chatHistory;
        }

        public List<FunctionDeclaration> getFunctions() {
            return functions;
        }
    }

    private static AuthorRole getAuthorRole(String role) {
        switch (role) {
            case "user":
                return AuthorRole.USER;
            case "assistant":
                return AuthorRole.ASSISTANT;
            case "system":
                return AuthorRole.SYSTEM;
            case "tool":
                return AuthorRole.TOOL;
            default:
                LOGGER.error("Unknown role: " + role);
                return AuthorRole.USER;
        }
    }

    private static ChatPromptParseVisitor<GeminiParsedPrompt> createVisitor() {
        return new ChatPromptParseVisitor<GeminiParsedPrompt>() {
            private GeminiParsedPrompt parsedRaw;
            private final List<FunctionDeclaration> functionDefinitions = new ArrayList<>();
            private final ChatHistory chatHistory = new ChatHistory();

            @Override
            public ChatPromptParseVisitor<GeminiParsedPrompt> addMessage(
                    String role,
                    String content) {
                chatHistory.addMessage(new ChatMessageContent<>(getAuthorRole(role), content));
                return this;
            }

            @Override
            public ChatPromptParseVisitor<GeminiParsedPrompt> addFunction(
                    String name,
                    @Nullable
                    String description,
                    @Nullable
                    BinaryData parameters) {

                String paramString = null;
                if (parameters != null) {
                    paramString = parameters.toString();
                }

                // TODO: Update parameters to not depend on Azure SDK
                Schema.Builder parametersBuilder = Schema.newBuilder();

                FunctionDeclaration.Builder function = FunctionDeclaration.newBuilder()
                        .setName(name)
                        .setDescription(description)
                        .setParameters(parametersBuilder.build());

                functionDefinitions.add(function.build());
                return this;
            }

            @Override
            public boolean areMessagesEmpty() {
                return chatHistory.getMessages().isEmpty();
            }

            @Override
            public ChatPromptParseVisitor<GeminiParsedPrompt> fromRawPrompt(
                    String rawPrompt) {

                ChatMessageContent<?> message = new ChatMessageContent<>(AuthorRole.USER, rawPrompt);

                this.parsedRaw = new GeminiParsedPrompt(new ChatHistory(Collections.singletonList(message)),null);

                return this;
            }

            @Override
            public GeminiParsedPrompt get() {
                if (parsedRaw != null) {
                    return parsedRaw;
                }

                return new GeminiParsedPrompt(chatHistory, functionDefinitions);
            }
        };
    }

    public static GeminiParsedPrompt parse(String rawPrompt) {
        ChatPromptParseVisitor<GeminiParsedPrompt> visitor = ChatXMLPromptParser.parse(
                rawPrompt,
                createVisitor()
        );

        return visitor.get();
    }
}
