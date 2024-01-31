package com.microsoft.semantickernel.services;

import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.stream.Collectors;

import javax.annotation.Nullable;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.microsoft.semantickernel.AIService;
import com.microsoft.semantickernel.Verify;
import com.microsoft.semantickernel.chatcompletion.ChatCompletionService;
import com.microsoft.semantickernel.orchestration.KernelFunction;
import com.microsoft.semantickernel.orchestration.KernelFunctionArguments;
import com.microsoft.semantickernel.orchestration.PromptExecutionSettings;
import com.microsoft.semantickernel.textcompletion.TextGenerationService;

public class OrderedAIServiceSelector extends BaseAIServiceSelector {

    private static final Logger LOGGER = LoggerFactory.getLogger(OrderedAIServiceSelector.class);

    public OrderedAIServiceSelector() {
        super(new HashMap<>());
    }

    public OrderedAIServiceSelector(Map<Class<? extends AIService>, AIService> services) {
        super(services);
    }

    @Nullable
    public <T extends AIService> AIServiceSelection<T> trySelectAIService(
        Class<T> serviceType,
        @Nullable
        KernelFunction function,
        @Nullable
        KernelFunctionArguments arguments,
        Map<Class<? extends AIService>, AIService> services) {

        // Allow the execution settings from the kernel arguments to take precedence
        Map<String, PromptExecutionSettings> executionSettings = 
            settingsFromFunctionSettings(function);

        if (executionSettings == null || executionSettings.isEmpty()) {
            AIService service = getAnyService(serviceType);
            if (service != null) {
                return castServiceSelection(new AIServiceSelection<>(service, null));
            }
        } else {
            AIServiceSelection<?> selection = executionSettings
                .entrySet()
                .stream()
                .map(keyValue -> {

                    PromptExecutionSettings settings = keyValue.getValue();
                    String serviceId = keyValue.getKey();

                    if (!Verify.isNullOrEmpty(serviceId)) {
                        AIService service = getService(serviceId);
                        if (service != null) {
                            return castServiceSelection(new AIServiceSelection<>(service, settings));
                        }
                    }

                    return null;
                })
                .filter(Objects::nonNull)
                .findFirst()
                .orElseGet(() -> null);

            if (selection != null) {
                return castServiceSelection(selection);
            }

            selection = executionSettings
                .entrySet()
                .stream()
                .map(keyValue -> {
                    PromptExecutionSettings settings = keyValue.getValue();

                    if (!Verify.isNullOrEmpty(settings.getModelId())) {
                        AIService service = getServiceByModelId(settings.getModelId());
                        if (service != null) {
                            return castServiceSelection(new AIServiceSelection<>(service, settings));
                        }
                    }

                    return null;
                })
                .filter(Objects::nonNull)
                .findFirst()
                .orElseGet(() -> null);

            if (selection != null) {
                return castServiceSelection(selection);
            }
        }

        AIService defaultService = getService(PromptExecutionSettings.DEFAULT_SERVICE_ID);

        if (defaultService != null && serviceType.isAssignableFrom(defaultService.getClass())) {
            return castServiceSelection(new AIServiceSelection<>(defaultService, null));
        }

        AIService service = getAnyService(serviceType);
        PromptExecutionSettings settings = null;

        if (executionSettings != null) {
            if (executionSettings.containsKey(PromptExecutionSettings.DEFAULT_SERVICE_ID)) {
                settings = executionSettings.get(PromptExecutionSettings.DEFAULT_SERVICE_ID);
            } else {
                settings = executionSettings.values().stream().findFirst().orElseGet(() -> null);
            }
        }

        if (service != null) {
            return castServiceSelection(new AIServiceSelection<>(service, settings));
        }

        LOGGER.warn("No service found meeting requirements");
        return null;
    }

    @SuppressWarnings("unchecked")
    private static <T extends AIService> AIServiceSelection<T> castServiceSelection(AIServiceSelection<?> selection) {
        try {
            // unchecked cast
            return (AIServiceSelection<T>) selection;
        } catch (ClassCastException e) {
            LOGGER.debug("%s", e.getMessage());
            return null;
        }
    }

    @Nullable
    private static Map<String, PromptExecutionSettings> settingsFromFunctionSettings(
        @Nullable
        KernelFunction function) {
        if (function != null) {
            return function.getExecutionSettings();
        }
        return null;
    }

    private AIService getServiceByModelId(String modelId) {
        return services
            .values()
            .stream()
            .filter(s -> modelId.equalsIgnoreCase(s.getModelId()))
            .findFirst()
            .orElseGet(() -> null);
    }


    @Nullable
    public AIService getService(String serviceId) {
        return services
            .values()
            .stream()
            .filter(s -> serviceId.equalsIgnoreCase(s.getServiceId()))
            .findFirst()
            .orElseGet(() -> null);
    }

    @Nullable
    public <T> T getService(Class<T> clazz) {
        T service = (T) services.get(clazz);

        if (service == null) {
            service = (T) services
                .entrySet()
                .stream()
                .filter(it -> clazz.isAssignableFrom(it.getKey()))
                .map(it -> it.getValue())
                .findFirst()
                .orElseGet(() -> null);
        }

        if (service == null &&
            (clazz.equals(TextGenerationService.class) ||
                clazz.equals(ChatCompletionService.class))) {
            LOGGER.warn(
                "Requested a non-existent service type of {}. Consider requesting a TextAIService instead.",
                clazz.getName());
        }

        return service;
    }

    AIService getAnyService(Class<? extends AIService> serviceType) {
        List<AIService> services = getServices(serviceType);
        if (services.isEmpty()) {
            return null;
        }
        return services.get(0);
    }

    private List<AIService> getServices(Class<? extends AIService> serviceType) {
        return
            services
                .entrySet()
                .stream()
                .filter(it -> serviceType.isAssignableFrom(it.getKey()))
                .map(Map.Entry::getValue)
                .collect(Collectors.toList());
    }

}
