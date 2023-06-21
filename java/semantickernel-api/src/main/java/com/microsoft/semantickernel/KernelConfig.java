// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel;

import com.microsoft.semantickernel.chatcompletion.ChatCompletion;
import com.microsoft.semantickernel.orchestration.SKFunction;
import com.microsoft.semantickernel.textcompletion.TextCompletion;

import java.util.*;
import java.util.function.Function;

import javax.annotation.Nullable;

public final class KernelConfig {

    private static final String DEFAULT_SERVICE_ID = "__SK_DEFAULT";
    private final Map<String, Function<Kernel, TextCompletion>> textCompletionServices;
    private final Map<String, Function<Kernel, ChatCompletion>> chatCompletionServices;
    private final ArrayList<SKFunction<?>> skills;

    public KernelConfig(
            Map<String, Function<Kernel, TextCompletion>> textCompletionServices,
            Map<String, Function<Kernel, ChatCompletion>> chatCompletionServices,
            List<SKFunction<?>> skills) {
        this.textCompletionServices = new HashMap<>();
        this.textCompletionServices.putAll(textCompletionServices);
        this.chatCompletionServices = new HashMap<>(chatCompletionServices);
        this.skills = new ArrayList<>(skills);
    }

    @Nullable
    public Function<Kernel, TextCompletion> getTextCompletionService(String serviceId) {
        return textCompletionServices.get(serviceId);
    }

    public List<SKFunction<?>> getSkills() {
        return Collections.unmodifiableList(skills);
    }

    public Function<Kernel, TextCompletion> getTextCompletionServiceOrDefault(
            @Nullable String serviceId) {
        if (serviceId == null) {
            serviceId = DEFAULT_SERVICE_ID;
        }

        if (!this.textCompletionServices.containsKey(serviceId)) {
            throw new KernelException(
                    KernelException.ErrorCodes.ServiceNotFound,
                    "A text completion service id '" + serviceId + "' doesn't exist");
        }

        return this.textCompletionServices.get(serviceId);
    }

    public Function<Kernel, ChatCompletion> getChatCompletionServiceOrDefault(
            @Nullable String serviceId) {
        if (serviceId == null) {
            serviceId = DEFAULT_SERVICE_ID;
        }

        if (!this.chatCompletionServices.containsKey(serviceId)) {
            throw new KernelException(
                    KernelException.ErrorCodes.ServiceNotFound,
                    "A chat completion service id '" + serviceId + "' doesn't exist");
        }

        return this.chatCompletionServices.get(serviceId);
    }

    public static class Builder {
        private Map<String, Function<Kernel, TextCompletion>> textCompletionServices =
                new HashMap<>();

        private List<SKFunction<?>> skillBuilders = new ArrayList<>();

        private final Map<String, Function<Kernel, ChatCompletion>> chatCompletionServices =
                new HashMap<>();

        public Builder addSkill(SKFunction<?> functionDefinition) {
            skillBuilders.add(functionDefinition);
            return this;
        }

        // TODO, is there a need for this to be a factory?
        public Builder addTextCompletionService(
                String serviceId, Function<Kernel, TextCompletion> serviceFactory) {
            if (serviceId == null || serviceId.isEmpty()) {
                throw new IllegalArgumentException("Null or empty serviceId");
            }

            textCompletionServices.put(serviceId, serviceFactory);

            if (textCompletionServices.size() == 1) {
                textCompletionServices.put(DEFAULT_SERVICE_ID, serviceFactory);
            }
            return this;
        }

        public Builder setDefaultTextCompletionService(String serviceId) {
            if (!this.textCompletionServices.containsKey(serviceId)) {
                throw new KernelException(
                        KernelException.ErrorCodes.ServiceNotFound,
                        "A text completion service id '" + serviceId + "' doesn't exist");
            }

            this.textCompletionServices.put(
                    DEFAULT_SERVICE_ID, textCompletionServices.get(serviceId));

            return this;
        }

        /**
         * Add to the list a service for chat completion, e.g. OpenAI ChatGPT.
         *
         * @param serviceId Id used to identify the service
         * @param serviceFactory Function used to instantiate the service object
         * @return Current object instance
         */
        public Builder addChatCompletionService(
                @Nullable String serviceId, Function<Kernel, ChatCompletion> serviceFactory) {
            if (serviceId != null
                    && serviceId.toUpperCase(Locale.ROOT).equals(DEFAULT_SERVICE_ID)) {
                String msg =
                        "The service id '"
                                + serviceId
                                + "' is reserved, please use a different name";
                throw new KernelException(
                        KernelException.ErrorCodes.InvalidServiceConfiguration, msg);
            }

            if (serviceId == null) {
                serviceId = DEFAULT_SERVICE_ID;
            }

            this.chatCompletionServices.put(serviceId, serviceFactory);
            if (this.chatCompletionServices.size() == 1) {
                this.chatCompletionServices.put(DEFAULT_SERVICE_ID, serviceFactory);
            }

            return this;
        }

        public KernelConfig build() {
            return new KernelConfig(
                    Collections.unmodifiableMap(textCompletionServices),
                    chatCompletionServices,
                    skillBuilders);
        }
    }
}
