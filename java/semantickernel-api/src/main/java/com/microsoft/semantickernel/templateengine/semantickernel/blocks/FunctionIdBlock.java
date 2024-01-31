// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.templateengine.semantickernel.blocks;

import javax.annotation.Nullable;

import com.microsoft.semantickernel.orchestration.KernelFunctionArguments;

public final class FunctionIdBlock extends Block implements TextRendering {
    private final String skillName;

    private final String functionName;

    public FunctionIdBlock(String content) {
        super(content, BlockTypes.FunctionId);

        String[] functionNameParts = this.getContent().split("\\.", -1);
        if (functionNameParts.length > 2) {
            throw new RuntimeException(
                    "A function name can contain at most one dot separating the skill name from the"
                            + " function name");
        }

        if (functionNameParts.length == 2) {
            this.skillName = functionNameParts[0];
            this.functionName = functionNameParts[1];
            return;
        }

        this.functionName = this.getContent();
        this.skillName = "";
    }

    @Override
    @Nullable
    public String render(KernelFunctionArguments variables) {
        return this.getContent();
    }

    @Override
    public boolean isValid() {
        if (!this.getContent().matches("^[a-zA-Z0-9_.]*$")) {
            // errorMsg = "The function identifier is empty";
            return false;
        }

        if (hasMoreThanOneDot(this.getContent())) {
            // errorMsg = "The function identifier can contain max one '.' char separating skill
            // name from function name";
            return false;
        }

        // errorMsg = "";
        return true;
    }

    private static boolean hasMoreThanOneDot(String value) {
        if (value == null || value.length() < 2) {
            return false;
        }

        return value.matches("^.*\\..*\\..*$");
    }

    public String getPluginName() {
        return skillName;
    }

    public String getFunctionName() {
        return functionName;
    }
}
