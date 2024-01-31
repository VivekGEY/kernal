package com.microsoft.semantickernel.samples.syntaxexamples;

import com.azure.ai.openai.OpenAIAsyncClient;
import com.azure.ai.openai.OpenAIClientBuilder;
import com.azure.core.credential.AzureKeyCredential;
import com.azure.core.credential.KeyCredential;
import com.microsoft.semantickernel.Kernel;
import com.microsoft.semantickernel.chatcompletion.ChatCompletionService;
import com.microsoft.semantickernel.orchestration.KernelFunction;
import com.microsoft.semantickernel.orchestration.KernelFunctionArguments;
import com.microsoft.semantickernel.orchestration.PromptExecutionSettings;
import com.microsoft.semantickernel.semanticfunctions.KernelFunctionFromPrompt;
import com.microsoft.semantickernel.semanticfunctions.PromptTemplateConfig;
import com.microsoft.semantickernel.textcompletion.TextGenerationService;
import java.util.Arrays;
import java.util.HashMap;

public class Example61_MultipleLLMs {

    private static final boolean USE_AZURE_CLIENT = Boolean.parseBoolean(
        System.getenv("USE_AZURE_CLIENT"));
    private static final String CLIENT_KEY = System.getenv("CLIENT_KEY");

    // Only required if USE_AZURE_CLIENT is true
    private static final String CLIENT_ENDPOINT = System.getenv("CLIENT_ENDPOINT");

    public static void main(String[] args) {
        System.out.println("======== Example61_MultipleLLMs ========");

        OpenAIAsyncClient client;

        if (USE_AZURE_CLIENT) {
            client = new OpenAIClientBuilder()
                .credential(new AzureKeyCredential(CLIENT_KEY))
                .endpoint(CLIENT_ENDPOINT)
                .buildAsyncClient();
        } else {
            client = new OpenAIClientBuilder()
                .credential(new KeyCredential(CLIENT_KEY))
                .buildAsyncClient();
        }
        System.out.println("======== Using Chat GPT model for text generation ========");

        var openAIChatCompletion = ChatCompletionService.builder()
            .withOpenAIAsyncClient(client)
            .withServiceId("AzureOpenAIChat")
            .withModelId("gpt-35-turbo-2")
            .build();

        var textGenerationService = TextGenerationService.builder()
            .withOpenAIAsyncClient(client)
            .withServiceId("AzureOpenAIText")
            .withModelId("text-davinci-003")
            .build();

        var kernel = Kernel.builder()
            .withAIService(ChatCompletionService.class, openAIChatCompletion)
            .withAIService(TextGenerationService.class, textGenerationService)
            .build();

        runByServiceIdAsync(kernel, "AzureOpenAIText");
        runByServiceIdAsync(kernel, "AzureOpenAIChat");

        runByModelIdAsync(kernel, "gpt-35-turbo-2");

        runByFirstModelIdAsync(kernel, "text-davinci-003", "gpt-35-turbo-2");
    }


    public static void runByServiceIdAsync(Kernel kernel, String serviceId) {
        System.out.println("======== Service Id: " + serviceId + " ========");

        var prompt = "Hello AI, what can you do for me?";

        KernelFunctionArguments arguments = KernelFunctionArguments.builder().build();

        KernelFunction func = KernelFunctionFromPrompt
            .builder()
            .withTemplate(prompt)
            .withDefaultExecutionSettings(
                PromptExecutionSettings.builder()
                    .withServiceId(serviceId)
                    .build()
            )
            .build();

        var result = kernel.invokeAsync(func, arguments, String.class).block();
        System.out.println(result.getResultVariable());
    }


    public static void runByModelIdAsync(Kernel kernel, String modelId) {
        System.out.println("======== Model Id: " + modelId + " ========");

        var prompt = "Hello AI, what can you do for me?";

        var result = kernel.invokeAsync(
            KernelFunctionFromPrompt
                .builder()
                .withTemplate(prompt)
                .withDefaultExecutionSettings(
                    PromptExecutionSettings.builder()
                        .withModelId(modelId)
                        .build()
                )
                .build(),
            KernelFunctionArguments.builder().build(),
            String.class
        ).block();

        System.out.println(result.getResultVariable());
    }


    public static void runByFirstModelIdAsync(Kernel kernel, String... modelIds) {
        System.out.println("======== Model Ids: " + String.join(",", modelIds) + " ========");

        var prompt = "Hello AI, what can you do for me?";

        var modelSettings = new HashMap<String, PromptExecutionSettings>();

        Arrays.stream(modelIds).forEach(
            modelId -> {
                modelSettings.put(
                    modelId,
                    PromptExecutionSettings.builder()
                        .withModelId(modelId)
                        .build());
            }
        );

        var promptConfig = new PromptTemplateConfig(prompt);
        promptConfig.setName("HelloAI");
        promptConfig.setExecutionSettings(modelSettings);

        var function = KernelFunctionFromPrompt.create(promptConfig);

        var result = kernel.invokeAsync(
                function,
                KernelFunctionArguments.builder().build(),
                String.class)
            .block();

        System.out.println(result.getResultVariable());
    }

}
