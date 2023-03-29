﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;

namespace Microsoft.SemanticKernel.Planning;

/// <summary>
/// <para>Semantic skill that evaluates conditional structures</para>
/// <para>
/// Usage:
/// var kernel = SemanticKernel.Build(ConsoleLogger.Log);
/// kernel.ImportSkill("conditional", new ConditionalSkill(kernel));
/// </para>
/// </summary>
public class ConditionalFlowHelper
{
    #region Prompts

    internal const string IfStructureCheckPrompt =
        @"Structure:
<if>
	<conditiongroup/>
	<then/>
	<else/>
</if>

Rules:
. A ""Condition Group"" must exists and have at least one ""Condition"" or ""Condition Group"" child
. Check recursively if any existing the children ""Condition Group"" follow the Rule 1
. ""Then"" must exists and have at least one child node
. ""Else"" is optional
. If ""Else"" is provided must have one or more children nodes
. Return true if Test Structure is valid
. Return false if Test Structure is not valid with a reason
. Give a json list of variables used inside all the ""Condition Group""s
. All the return should be in Json format.
. Response Json Structure:
{
  ""valid"": bool,
  ""reason"": string,
  ""variables"": [string] (only the variables within ""Condition Group"")
}

Test Structure:
{{$IfStatementContent}}";

    internal const string EvaluateConditionPrompt =
        @"Rules
1 ""and"" and ""or"" should be self closing tags
2 Expect should be TRUE, FALSE OR ERROR

Given Example:
<conditiongroup>
    <condition variable=""$x"" exact=""1"" />
    <and/>
    <condition variable=""$y"" contains=""asd"" />
    <or/>
    <not>
        <condition variable=""$z"" greaterthan=""10"" />
    </not>
</conditiongroup>

Expect Example: TRUE

Evaluate Example:
(x == ""1"" ^ y.Contains(""asd"") ∨ ¬(z > 10))
(TRUE ^ TRUE ∨ ¬ (FALSE))
(TRUE ^ TRUE ∨ TRUE) = TRUE

Variables Example:
x = ""2""
y = ""adfsdasfgsasdddsf""
z = ""100""
w = ""sdf""

Given Example:
<if>
     <conditiongroup>
          <condition variable=""$24hour"" exact=""1"" />
          <and/>
          <condition variable=""$24hour"" greaterthan=""10"" />
     </conditiongroup>
     <then><if>Good Morning</if></then>
     <else><else>Good afternoon</else></else>
</if>

Variables Example:
24hour = 11

Expect Example: FALSE

Evaluate Example:
(24hour == 1 ^ 24hour > 10)
(FALSE ^ TRUE) = FALSE


Given Example:
Invalid XML

Variables Example:
24hour = 11

Expect Example: ERROR
Reason Example: 

Given Example:
<conditiongroup>
<condition variable=""$23hour"" exact=""10""/>
</conditiongroup>

Variables Example:
24hour = 11

Expect Example: ERROR
Reason Example: 23hour is not a valid variable. 

Given:
{{$IfCondition}}

Variables:
{{$ConditionalVariables}}

Expect: ";

    internal const string ExtractThenOrElseFromIfPrompt =
        @"Consider the below structure, ignore any format error:

{{$IfStatementContent}}

Rules:
Don't ignore non xml 
Invalid XML is part of the content

Now, write the exact content inside the first child ""{{$EvaluateIfBranchTag}}"" from the root If element

The exact content inside the first child ""{{$EvaluateIfBranchTag}}"" element from the root If element is:

";

    #endregion

    internal const string ReasonIdentifier = "Reason:";
    internal const string NoReasonMessage = "No reason was provided";

    private readonly ISKFunction _ifStructureCheckFunction;
    private readonly ISKFunction _evaluateConditionFunction;
    private readonly ISKFunction _evaluateIfBranchFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionalFlowHelper"/> class.
    /// </summary>
    /// <param name="kernel"> The kernel to use </param>
    /// <param name="completionBackend"> A optional completion backend to run the internal semantic functions </param>
    internal ConditionalFlowHelper(IKernel kernel, ITextCompletion? completionBackend = null)
    {
        this._ifStructureCheckFunction = kernel.CreateSemanticFunction(
            IfStructureCheckPrompt,
            skillName: nameof(ConditionalFlowHelper),
            description: "Evaluate if an If structure is valid and returns TRUE or FALSE",
            maxTokens: 100,
            temperature: 0,
            topP: 0.5);

        this._evaluateConditionFunction = kernel.CreateSemanticFunction(
            EvaluateConditionPrompt,
            skillName: nameof(ConditionalFlowHelper),
            description: "Evaluate a condition group and returns TRUE or FALSE",
            maxTokens: 100,
            temperature: 0,
            topP: 0.5);

        this._evaluateIfBranchFunction = kernel.CreateSemanticFunction(
            ExtractThenOrElseFromIfPrompt,
            skillName: nameof(ConditionalFlowHelper),
            description: "Extract the content of the first child tag from the root If element",
            maxTokens: 1000,
            temperature: 0,
            topP: 0.5);

        if (completionBackend is not null)
        {
            this._ifStructureCheckFunction.SetAIBackend(() => completionBackend);
            this._evaluateIfBranchFunction.SetAIBackend(() => completionBackend);
            this._evaluateConditionFunction.SetAIBackend(() => completionBackend);
        }
    }

    /// <summary>
    /// Get a planner if statement content and output then or else contents depending on the conditional evaluation.
    /// </summary>
    /// <param name="ifContent">If statement content.</param>
    /// <param name="context"> The context to use </param>
    /// <returns>Then or Else contents depending on the conditional evaluation</returns>
    /// <remarks>
    /// This skill is initially intended to be used only by the Plan Runner.
    /// </remarks>
    public async Task<SKContext> IfAsync(string ifContent, SKContext context)
    {
        var usedVariables = await this.GetVariablesAndEnsureIfStructureIsValidAsync(ifContent, context).ConfigureAwait(false);

        bool conditionEvaluation = await this.EvaluateConditionAsync(ifContent, usedVariables, context).ConfigureAwait(false);

        return await this.GetThenOrElseBranchAsync(ifContent, conditionEvaluation, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the content from the Then or the Else branch based in the condition provided
    /// </summary>
    /// <param name="ifContent">If Structure content</param>
    /// <param name="trueCondition">Condition used to decide on Then or Else returning data</param>
    /// <param name="context">Current context</param>
    /// <returns>SKContext with the input as the Then or the Else branches data</returns>
    private async Task<SKContext> GetThenOrElseBranchAsync(string ifContent, bool trueCondition, SKContext context)
    {
        var branchVariables = new ContextVariables(ifContent);
        context.Variables.Set("EvaluateIfBranchTag", trueCondition ? "Then" : "Else");
        context.Variables.Set("IfStatementContent", ifContent);

        return (await this._evaluateIfBranchFunction.InvokeAsync(context).ConfigureAwait(false));
    }

    /// <summary>
    /// Get the variables used in the If statement and ensure the structure is valid
    /// </summary>
    /// <param name="ifContent">If structure content</param>
    /// <param name="context">Current context</param>
    /// <returns>List of used variables in the if condition</returns>
    /// <exception cref="ConditionException">InvalidStatementStructure</exception>
    /// <exception cref="ConditionException">JsonResponseNotFound</exception>
    private async Task<IEnumerable<string>> GetVariablesAndEnsureIfStructureIsValidAsync(string ifContent, SKContext context)
    {
        context.Variables.Set("IfStatementContent", ifContent);
        var llmCheckFunctionResponse = (await this._ifStructureCheckFunction.InvokeAsync(ifContent, context).ConfigureAwait(false)).ToString();

        JsonNode llmResponse = this.IfCheckResponseAsJson(llmCheckFunctionResponse);
        var valid = llmResponse["valid"]!.GetValue<bool>();

        if (!valid)
        {
            throw new ConditionException(ConditionException.ErrorCodes.InvalidStatementStructure, llmResponse?["reason"]?.GetValue<string>() ?? NoReasonMessage);
        }

        // Get all variables from the json array and remove the $ prefix, return empty list if no variables are found
        var usedVariables = llmResponse["variables"]?.Deserialize<string[]>()?
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.TrimStart('$'))
                            ?? Enumerable.Empty<string>();

        return usedVariables;
    }

    /// <summary>
    /// Evaluates a condition group and returns TRUE or FALSE
    /// </summary>
    /// <param name="ifContent">If structure content</param>
    /// <param name="usedVariables">Used variables to send for evaluation</param>
    /// <param name="context">Current context</param>
    /// <returns>Condition result</returns>
    /// <exception cref="ConditionException">InvalidConditionFormat</exception>
    /// <exception cref="ConditionException">ContextVariablesNotFound</exception>
    private async Task<bool> EvaluateConditionAsync(string ifContent, IEnumerable<string> usedVariables, SKContext context)
    {
        var conditionContent = this.ExtractConditionalContent(ifContent);

        context.Variables.Set("IfCondition", conditionContent);
        context.Variables.Set("ConditionalVariables", this.GetConditionalVariablesFromContext(usedVariables, context.Variables));

        var llmConditionResponse = (await this._evaluateConditionFunction.InvokeAsync(conditionContent, context).ConfigureAwait(false))
            .ToString()
            .Trim();

        var reason = this.GetReason(llmConditionResponse);
        var error = !Regex.Match(llmConditionResponse.Trim(), @"^(true|false)", RegexOptions.IgnoreCase).Success;
        if (error)
        {
            throw new ConditionException(ConditionException.ErrorCodes.InvalidConditionFormat, reason);
        }

        return llmConditionResponse.StartsWith("TRUE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the condition root group content closest the If structure
    /// </summary>
    /// <param name="ifContent">If structure content to extract from</param>
    /// <returns>Conditiongroup contents</returns>
    private string ExtractConditionalContent(string ifContent)
    {
        XmlDocument xmlDoc = new();
        xmlDoc.LoadXml("<xml>" + ifContent + "</xml>");

        XmlNode parentConditionGroupNode =
            xmlDoc.SelectSingleNode("//if/conditiongroup")
            ?? throw new ConditionException(ConditionException.ErrorCodes.InvalidConditionFormat, "Conditiongroup definition is not present");

        return parentConditionGroupNode.OuterXml;
    }

    /// <summary>
    /// Gets all the variables used in the condition and their values from the context
    /// </summary>
    /// <param name="usedVariables">Variables used in the condition</param>
    /// <param name="variables">Context variables</param>
    /// <returns>List of variables and its values for prompting</returns>
    /// <exception cref="ConditionException">ContextVariablesNotFound</exception>
    private string GetConditionalVariablesFromContext(IEnumerable<string> usedVariables, ContextVariables variables)
    {
        var checkNotFoundVariables = usedVariables.Where(u => !variables.ContainsKey(u)).ToArray();
        if (checkNotFoundVariables.Any())
        {
            throw new ConditionException(ConditionException.ErrorCodes.ContextVariablesNotFound, string.Join(", ", checkNotFoundVariables));
        }

        var existingVariables = variables.Where(v => usedVariables.Contains(v.Key));

        var conditionalVariables = new StringBuilder();
        foreach (var v in existingVariables)
        {
            // Numeric don't add quotes
            var value = Regex.IsMatch(v.Value, "^[0-9.,]+$") ? v.Value : JsonSerializer.Serialize(v.Value);
            conditionalVariables.AppendLine($"{v.Key} = {value}");
        }

        return conditionalVariables.ToString();
    }

    /// <summary>
    /// Gets the reason from the LLM response
    /// </summary>
    /// <param name="llmResponse">Raw LLM response</param>
    /// <returns>Reason details</returns>
    private string? GetReason(string llmResponse)
    {
        var hasReasonIndex = llmResponse.IndexOf(ReasonIdentifier, StringComparison.OrdinalIgnoreCase);
        if (hasReasonIndex > -1)
        {
            return llmResponse[(hasReasonIndex + ReasonIdentifier.Length)..].Trim();
        }

        return NoReasonMessage;
    }

    /// <summary>
    /// Gets a JsonNode traversable structure from the LLM text response
    /// </summary>
    /// <param name="llmResponse"></param>
    /// <returns></returns>
    /// <exception cref="ConditionException"></exception>
    private JsonNode IfCheckResponseAsJson(string llmResponse)
    {
        var startIndex = llmResponse?.IndexOf('{', StringComparison.InvariantCultureIgnoreCase) ?? -1;
        JsonNode? response = null;

        if (startIndex > -1)
        {
            var jsonResponse = llmResponse![startIndex..];
            response = JsonSerializer.Deserialize<JsonNode>(jsonResponse);
        }

        if (response is null)
        {
            throw new ConditionException(ConditionException.ErrorCodes.JsonResponseNotFound);
        }

        return response;
    }
}
