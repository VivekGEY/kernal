﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Examples;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Xunit;
using Xunit.Abstractions;

namespace KernelSyntaxExamples;

// This example shows how to use Handlebars different syntax options like:
//  - set (works)
//  - function calling (works)
//  - loops (each) - not working at the moment, but tries to do so over a complex object generated by one of the functions: a JSON array-
//  - array - not working at the moment - to accumulate the results of the loop in an array
//  - conditionals (works)
//  - concatenation (works)
// In order to create a Prompt Function that fully benefits from the Handlebars syntax power.
// The example also shows how to use the HandlebarsPlanner to generate a plan (and persist it) which was used to generate the initial Handlebar template.
// The example also shows how to create two prompt functions and a plugin to group them together.
public class Example77_HandlebarsPromptSyntax : BaseTest
{
    [Fact]
    public async Task RunAsync()
    {
        this.WriteLine("======== LLMPrompts ========");

        string openAIModelId = TestConfiguration.OpenAI.ChatModelId;
        string openAIApiKey = TestConfiguration.OpenAI.ApiKey;

        if (openAIApiKey == null)
        {
            this.WriteLine("OpenAI credentials not found. Skipping example.");
            return;
        }

        Kernel kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: openAIModelId,
                apiKey: openAIApiKey)
            .Build();

        KernelFunction kernelFunctionGenerateProductNames =
            KernelFunctionFactory.CreateFromPrompt(
                "Given the company description, generate five different product names in name and function " +
                "that match the company description. " +
                "Ensure that they match the company and are aligned to it. " +
                "Think of products this company would really make and also have market potential. " +
                "Be original and do not make too long names or use more than 3-4 words for them." +
                "Also, the product name should be catchy and easy to remember. " +
                // JSON or NOT JSON, that is the question...                                
                "Output the product names and short descriptions as a bullet point list. " +
                //"Output the product names in a JSON array inside a JSON object named products. " +
                //"On them use the name and description as keys." +
                //"Ensure the JSON is well formed and is valid" +
                "The company description: {{$input}}",
                functionName: "GenerateProductNames",
                description: "Generate five product names to match a company.");

        KernelFunction kernelFunctionGenerateProductDescription =
            KernelFunctionFactory.CreateFromPrompt(
                "Given a product name and initial description, generate a description which is compelling, " +
                "engaging and stunning. Also add at the end how would you approach to develop, create it," +
                "with Semantic Kernel." +
                "Think of marketing terms and use positive words but do not lie and oversell. " +
                "Be original and do not make a too long description or use more than 2 paragraphs for it." +
                "Also, the product description should be catchy and easy to remember. " +
                "Output the description followed by the development approach preceded by Development approach: " +
                "The product name and description: {{$input}}",
                functionName: "GenerateProductCompellingDescription",
                description: "Generate a compelling product description for a product name and initial description.");

        KernelPlugin productMagicianPlugin =
            KernelPluginFactory.CreateFromFunctions(
                "productMagician",
                "Helps create a set of products for a company and descriptions for them.",
                new[] {
                    kernelFunctionGenerateProductNames,
                    kernelFunctionGenerateProductDescription
                });

        kernel.Plugins.Add(productMagicianPlugin);

        string companyDescription = "The company is a startup that is building new AI solutions for the market. using Generative AI and AI orchestration novel techonlogies. The company is an expert on this recently launched SDK (Software Development Toolkit) named Semantic Kernel. Semantic Kernel or SK, enables AI Orchestration with .NET which is production ready, enterprise ready and cloud ready." +
            "Also it is able to self plan and execute complex tasks and use the power of AI agents which" +
            "enables to divide-and-conquer complex problems between different entities that specialize in " +
            "concrete tasks like for example project management, coding and creating tests as well as other" +
            " agents can be responsible for executing the tests and assessing the code delivered and iterate" +
            " - this means creating feedback loops until the quality levels are met. The company is thinking of using AI Agent programming on coding, writing and project planning, and anything where AI Agents" +
            " can be applied and revolutionize a process or market niche.";

        //////////////////////////////////////////////////////////////////////////////////////////////////////
        //// Testing a the 5 product name generation
        //var productsResult =
        //    await kernel.InvokeAsync(kernelFunctionGenerateProductNames,
        //    new() {
        //      { "input", companyDescription }
        //    });
        //This.WriteLine($"Result: {productsResult}");

        //////////////////////////////////////////////////////////////////////////////////////////////////////
        // Testing the product description generation
        //string ProductDescription = "Product name: Skynet SDK Product description:A powerful .NET SDK destined to empower developers with advanced AI orchestration capabilities, capable of handling complex task automation.";
        //var productDescriptionResult =
        //    await kernel.InvokeAsync(kernelFunctionGenerateProductDescription,
        //    new() {
        //      { "input", ProductDescription }
        //    });
        //This.WriteLine($"Result: {productDescriptionResult}");

        // Using the planner to generate a plan for the user
        string userPrompt =
            "Using as input the following company description:" +
            "---" +
            " {{input}}" +
            "---" +
            "I want to generate five product names and engaging descriptions for a company." +
            "For this, I suggest the following process:" +
            "1. Please create first the product names given the company description" +
            "2. Then, for each of the 5 provided product names and descriptions, provided as a bullet point list, " +
            "generate the compelling, engaging, description" +
            "Please while doing this provide all the information as input:" +
            "For point 2, concatenate the product name to the rough description." +
            "Please use the format Product name: productname Description: description substituting" +
            "productname and description by the product names provided on point 1" +
            "Finally output all the product names and engaging descriptions preceded by PRODUCT 1: for the first" +
            "PRODUCT 2: for the second, and so on.";

        //////////////////////////////////////////////////////////////////////////////////////////////////////
        // Create the plan
        //var planner = new HandlebarsPlanner(new HandlebarsPlannerOptions() { AllowLoops = true });
        //var plan = await planner.CreatePlanAsync(kernel, userPrompt);
        //var planName = "plan4ProductsGeneration.txt";

        //// Print the plan to the console
        //This.WriteLine($"Plan: {plan}");

        //var serializedPlan = plan.ToString();
        //await File.WriteAllTextAsync(planName, serializedPlan);
        //string retrievedPlan = await File.ReadAllTextAsync(planName);
        //plan = new HandlebarsPlan(serializedPlan);

        //////////////////////////////////////////////////////////////////////////////////////////////////////
        /// One of the generated plans of the code above:
        // Plan: {{!-- Step 1: Save the input company description to a variable --}}
        //{{set "companyDescription" input}}

        //{{!-- Step 2: Generate product names based on the company description --}}
        //{{set "productNames" (productMagician-GenerateProductNames input=companyDescription)}}

        //{{!-- Step 3: Loop over the generated product names to generate product descriptions--}}
        //{{set "products" (array)}}
        //{{#each productNames}}
        //  {{set "productName" this}}
        //  {{set "roughDescription" (concat "Product related to " companyName)}}
        //  {{set "productDescription" (productMagician-GenerateProductCompellingDescription input=roughDescription)}}
        //  {{set "products" (array.push products (concat "Product name: " productName " Description: " productDescription))}}
        //{{/each}}

        //{{!-- Step 4: Loop over the final products array to print --}}
        //{{#each products}}
        //  {{set "index" (Add @index 1)}}
        //  {{json (concat "PRODUCT " index ": " this)}}
        //{{/each}}       

        //////////////////////////////////////////////////////////////////////////////////////////////////////
        // We will use one of the generated HandlebarsTemplate plan by the above code, with some modifications,
        // to highlight better the HandlebarsTemplate syntax usage.
        // And invoke it as a Prompt Function
        string handlebarsTemplate = @"
            {{!-- example of set with input and function calling with two syntax types --}}
            {{set ""companyDescription"" input}}
            {{set ""productNames"" (productMagician-GenerateProductNames companyDescription)}}
            {{set ""productNames2"" (productMagician-GenerateProductNames input=companyDescription)}}

            {{#if generateEngagingDescriptions}} 

                {{!-- Step 2: Create array for storing final descriptions --}}
                {{set ""finalDescriptions"" (array)}}
                {{set ""finalDescriptionsV2"" ""- PRODUCTS AND ENGAGING DESCRIPTIONS -""}}

                {{!-- Step 3: Iterate over each generated product name --}}
                {{#each productNames}}
                  {{!-- Step 3.1: Concatenating productName to initial company description --}}
                  {{set ""productDescription"" (concat ""Product Name: "" this.name "" Description: "" this.description)}}

                  {{!-- Step 3.2: Generate compelling description for each productName --}}
                  {{set ""compellingDescription"" (productMagician-GenerateProductCompellingDescription productDescription)}}

                  {{!-- Step 3.3: Concatenate compelling description and product number --}}                  
                  {{set ""outputDescription"" (concat ""PRODUCT :"" this.name "" Engaging Description: "" compellingDescription)}}

                  {{!-- Step 3.4: Add output description to the list --}}
                  {{set ""finalDescriptions"" (array finalDescriptions outputDescription)}}
                  {{set ""finalDescriptionsV2"" (concat finalDescriptionsV2 "" -- "" outputDescription)}}

                {{/each}}

                {{!-- Step 4: Print all product names and compelling descriptions --}}
                {{json finalDescriptionsV2}}
    
            {{else}} 
                {{!-- Example of concatenating text and variables to finally output it with json --}}
                {{set ""finalOutput"" (concat ""Description 1: "" productNames "" Description 2: "" productNames2)}}
                {{json finalOutput}}
            {{/if}}";

        /// did not work:
        /// {{set ""outputDescription"" (concat ""PRODUCT "" (Add @index 1) "": "" compellingDescription)}}
        /// Reason: helper add not found
        /// Also the array thing doesn't seem to work
        ///     {{set ""finalDescriptions"" (array)}}
        /// what fails on the each loop, when I use it I set generateEngagingDescriptions and make the prompt function output
        /// JSON to iterate through it. Technically it is the proper syntax but somehow it fails. some returns from the LLM are
        /// absolute hallucinations :/P
        var HandlebarsSPromptFunction = kernel.CreateFunctionFromPrompt(
            new()
            {
                Template = handlebarsTemplate,
                TemplateFormat = "handlebars"
            },
            new HandlebarsPromptTemplateFactory()
        );

        // Invoke prompt
        var result = await kernel.InvokeAsync(
                    HandlebarsSPromptFunction,
                    new() {
                        { "input", companyDescription },
                        { "generateEngagingDescriptions", false }
                    }
                );

        this.WriteLine($"Result:  {result}");
    }

    public Example77_HandlebarsPromptSyntax(ITestOutputHelper output) : base(output)
    {
    }
}
