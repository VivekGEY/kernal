// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.textcompletion;

import com.microsoft.semantickernel.orchestration.SKContext;
import com.microsoft.semantickernel.orchestration.SKFunction;
import com.microsoft.semantickernel.semanticfunctions.PromptTemplateConfig;
import com.microsoft.semantickernel.semanticfunctions.SemanticFunctionConfig;

import reactor.core.publisher.Mono;

import java.util.List;

import javax.annotation.Nullable;

public interface CompletionSKFunction extends SKFunction<CompletionRequestSettings> {

    /**
     * Method to aggregate partitioned results of a semantic function.
     *
     * @param partitionedInput Input to aggregate
     * @param context Semantic Kernel context
     * @return Aggregated results
     */
    Mono<SKContext> aggregatePartitionedResultsAsync(
            List<String> partitionedInput, @Nullable SKContext context);

    abstract class Builder {

        protected Builder() {}

        public abstract CompletionSKFunction createFunction(
                String promptTemplate,
                PromptTemplateConfig config,
                String functionName,
                @Nullable String skillName);

        public abstract CompletionSKFunction createFunction(
                String functionName, SemanticFunctionConfig functionConfig);

        public abstract CompletionSKFunction createFunction(
                @Nullable String skillNameFinal,
                String functionName,
                SemanticFunctionConfig functionConfig);

        public abstract CompletionSKFunction createFunction(
                String promptTemplate,
                @Nullable String functionName,
                @Nullable String skillName,
                @Nullable String description);

        public abstract CompletionSKFunction createFunction(
                String prompt,
                @Nullable String functionName,
                @Nullable String skillName,
                @Nullable String description,
                PromptTemplateConfig.CompletionConfig completionConfig);
    }
}
