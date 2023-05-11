package com.microsoft.semantickernel;

import com.microsoft.openai.OpenAIAsyncClient;
import com.microsoft.semantickernel.extensions.KernelExtensions;
import com.microsoft.semantickernel.planner.sequentialplanner.SequentialPlanner;
import java.io.IOException;

public class Example05UsingThePlanner {

  public static SequentialPlanner getPlanner(Kernel kernel) {
    kernel.importSkills("SummarizeSkill", KernelExtensions.importSemanticSkillFromDirectory(
        "samples/skills", "SummarizeSkill"));
    kernel.importSkills("WriterSkill", KernelExtensions.importSemanticSkillFromDirectory(
        "samples/skills", "WriterSkill"));

    return new SequentialPlanner(kernel, null, null);
  }

  public static void run (boolean useAzureOpenAI) {
    OpenAIAsyncClient client = Example00GettingStarted.getClient(useAzureOpenAI);
    Kernel kernel = Example00GettingStarted.getKernel(client);

    SequentialPlanner planner = getPlanner(kernel);
    System.out.println(planner.createPlanAsync(
            "Write a poem about John Doe, then translate it into Italian.")
        .block().getResult());

    // TODO: execute the plan
  }

  public static void main (String[] args) {
    run(false);
  }
}
