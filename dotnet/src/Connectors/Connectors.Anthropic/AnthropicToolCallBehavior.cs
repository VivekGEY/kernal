﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Microsoft.SemanticKernel.Connectors.Anthropic.Core;

namespace Microsoft.SemanticKernel.Connectors.Anthropic;

/// <summary>Represents a behavior for Claude tool calls.</summary>
public abstract class AnthropicToolCallBehavior
{
    // NOTE: Right now, the only tools that are available are for function calling. In the future,
    // this class can be extended to support additional kinds of tools, including composite ones:
    // the ClaudePromptExecutionSettings has a single ToolCallBehavior property, but we could
    // expose a `public static ToolCallBehavior Composite(params ToolCallBehavior[] behaviors)`
    // or the like to allow multiple distinct tools to be provided, should that be appropriate.
    // We can also consider additional forms of tools, such as ones that dynamically examine
    // the Kernel, KernelArguments, etc., and dynamically contribute tools to the ChatCompletionsOptions.

    /// <summary>
    /// The default maximum number of tool-call auto-invokes that can be made in a single request.
    /// </summary>
    /// <remarks>
    /// After this number of iterations as part of a single user request is reached, auto-invocation
    /// will be disabled (e.g. <see cref="AutoInvokeKernelFunctions"/> will behave like <see cref="EnableKernelFunctions"/>)).
    /// This is a safeguard against possible runaway execution if the model routinely re-requests
    /// the same function over and over. It is currently hardcoded, but in the future it could
    /// be made configurable by the developer. Other configuration is also possible in the future,
    /// such as a delegate on the instance that can be invoked upon function call failure (e.g. failure
    /// to find the requested function, failure to invoke the function, etc.), with behaviors for
    /// what to do in such a case, e.g. respond to the model telling it to try again. With parallel tool call
    /// support, where the model can request multiple tools in a single response, it is significantly
    /// less likely that this limit is reached, as most of the time only a single request is needed.
    /// </remarks>
    private const int DefaultMaximumAutoInvokeAttempts = 5;

    /// <summary>
    /// Gets an instance that will provide all of the <see cref="Kernel"/>'s plugins' function information.
    /// Function call requests from the model will be propagated back to the caller.
    /// </summary>
    /// <remarks>
    /// If no <see cref="Kernel"/> is available, no function information will be provided to the model.
    /// </remarks>
    public static AnthropicToolCallBehavior EnableKernelFunctions => new KernelFunctions(autoInvoke: false);

    /// <summary>
    /// Gets an instance that will both provide all of the <see cref="Kernel"/>'s plugins' function information
    /// to the model and attempt to automatically handle any function call requests.
    /// </summary>
    /// <remarks>
    /// When successful, tool call requests from the model become an implementation detail, with the service
    /// handling invoking any requested functions and supplying the results back to the model.
    /// If no <see cref="Kernel"/> is available, no function information will be provided to the model.
    /// </remarks>
    public static AnthropicToolCallBehavior AutoInvokeKernelFunctions => new KernelFunctions(autoInvoke: true);

    /// <summary>Gets an instance that will provide the specified list of functions to the model.</summary>
    /// <param name="functions">The functions that should be made available to the model.</param>
    /// <param name="autoInvoke">true to attempt to automatically handle function call requests; otherwise, false.</param>
    /// <returns>
    /// The <see cref="AnthropicToolCallBehavior"/> that may be set into <see cref="AnthropicToolCallBehavior"/>
    /// to indicate that the specified functions should be made available to the model.
    /// </returns>
    public static AnthropicToolCallBehavior EnableFunctions(IEnumerable<AnthropicFunction> functions, bool autoInvoke = false)
    {
        Verify.NotNull(functions);
        return new EnabledFunctions(functions, autoInvoke);
    }

    /// <summary>Initializes the instance; prevents external instantiation.</summary>
    private AnthropicToolCallBehavior(bool autoInvoke)
    {
        this.MaximumAutoInvokeAttempts = autoInvoke ? DefaultMaximumAutoInvokeAttempts : 0;
    }

    /// <summary>Gets how many requests are part of a single interaction should include this tool in the request.</summary>
    /// <remarks>
    /// This should be greater than or equal to <see cref="MaximumAutoInvokeAttempts"/>. It defaults to <see cref="int.MaxValue"/>.
    /// Once this limit is reached, the tools will no longer be included in subsequent retries as part of the operation, e.g.
    /// if this is 1, the first request will include the tools, but the subsequent response sending back the tool's result
    /// will not include the tools for further use.
    /// </remarks>
    public int MaximumUseAttempts { get; } = int.MaxValue;

    /// <summary>Gets how many tool call request/response roundtrips are supported with auto-invocation.</summary>
    /// <remarks>
    /// To disable auto invocation, this can be set to 0.
    /// </remarks>
    public int MaximumAutoInvokeAttempts { get; }

    /// <summary>
    /// Gets whether validation against a specified list is required before allowing the model to request a function from the kernel.
    /// </summary>
    /// <value>true if it's ok to invoke any kernel function requested by the model if it's found;
    /// false if a request needs to be validated against an allow list.</value>
    internal virtual bool AllowAnyRequestedKernelFunction => false;

    /// <summary>Configures the <paramref name="request"/> with any tools this <see cref="AnthropicToolCallBehavior"/> provides.</summary>
    /// <param name="kernel">The <see cref="Kernel"/> used for the operation.
    /// This can be queried to determine what tools to provide into the <paramref name="request"/>.</param>
    /// <param name="request">The destination <see cref="AnthropicRequest"/> to configure.</param>
    internal abstract void ConfigureClaudeRequest(Kernel? kernel, AnthropicRequest request);

    internal AnthropicToolCallBehavior Clone()
    {
        return (AnthropicToolCallBehavior)this.MemberwiseClone();
    }

    /// <summary>
    /// Represents a <see cref="AnthropicToolCallBehavior"/> that will provide to the model all available functions from a
    /// <see cref="Kernel"/> provided by the client.
    /// </summary>
    internal sealed class KernelFunctions : AnthropicToolCallBehavior
    {
        internal KernelFunctions(bool autoInvoke) : base(autoInvoke) { }

        public override string ToString() => $"{nameof(KernelFunctions)}(autoInvoke:{this.MaximumAutoInvokeAttempts != 0})";

        internal override void ConfigureClaudeRequest(Kernel? kernel, AnthropicRequest request)
        {
            // If no kernel is provided, we don't have any tools to provide.
            if (kernel is null)
            {
                return;
            }

            // Provide all functions from the kernel.
            foreach (var functionMetadata in kernel.Plugins.GetFunctionsMetadata())
            {
                request.AddFunction(FunctionMetadataAsClaudeFunction(functionMetadata));
            }
        }

        internal override bool AllowAnyRequestedKernelFunction => true;

        /// <summary>
        /// Convert a <see cref="KernelFunctionMetadata"/> to an <see cref="AnthropicFunction"/>.
        /// </summary>
        /// <param name="metadata">The <see cref="KernelFunctionMetadata"/> object to convert.</param>
        /// <returns>An <see cref="AnthropicFunction"/> object.</returns>
        private static AnthropicFunction FunctionMetadataAsClaudeFunction(KernelFunctionMetadata metadata)
        {
            IReadOnlyList<KernelParameterMetadata> metadataParams = metadata.Parameters;

            var openAIParams = new ClaudeFunctionParameter[metadataParams.Count];
            for (int i = 0; i < openAIParams.Length; i++)
            {
                var param = metadataParams[i];

                openAIParams[i] = new ClaudeFunctionParameter(
                    param.Name,
                    GetDescription(param),
                    param.IsRequired,
                    param.ParameterType,
                    param.Schema);
            }

            return new AnthropicFunction(
                metadata.PluginName,
                metadata.Name,
                metadata.Description,
                openAIParams,
                new ClaudeFunctionReturnParameter(
                    metadata.ReturnParameter.Description,
                    metadata.ReturnParameter.ParameterType,
                    metadata.ReturnParameter.Schema));

            static string GetDescription(KernelParameterMetadata param)
            {
                string? stringValue = InternalTypeConverter.ConvertToString(param.DefaultValue);
                return !string.IsNullOrEmpty(stringValue) ? $"{param.Description} (default value: {stringValue})" : param.Description;
            }
        }
    }

    /// <summary>
    /// Represents a <see cref="AnthropicToolCallBehavior"/> that provides a specified list of functions to the model.
    /// </summary>
    internal sealed class EnabledFunctions : AnthropicToolCallBehavior
    {
        private readonly AnthropicFunction[] _functions;

        public EnabledFunctions(IEnumerable<AnthropicFunction> functions, bool autoInvoke) : base(autoInvoke)
        {
            this._functions = functions.ToArray();
        }

        public override string ToString() =>
            $"{nameof(EnabledFunctions)}(autoInvoke:{this.MaximumAutoInvokeAttempts != 0}): " +
            $"{string.Join(", ", this._functions.Select(f => f.FunctionName))}";

        internal override void ConfigureClaudeRequest(Kernel? kernel, AnthropicRequest request)
        {
            if (this._functions.Length == 0)
            {
                return;
            }

            bool autoInvoke = this.MaximumAutoInvokeAttempts > 0;

            // If auto-invocation is specified, we need a kernel to be able to invoke the functions.
            // Lack of a kernel is fatal: we don't want to tell the model we can handle the functions
            // and then fail to do so, so we fail before we get to that point. This is an error
            // on the consumers behalf: if they specify auto-invocation with any functions, they must
            // specify the kernel and the kernel must contain those functions.
            if (autoInvoke && kernel is null)
            {
                throw new KernelException($"Auto-invocation with {nameof(EnabledFunctions)} is not supported when no kernel is provided.");
            }

            foreach (var func in this._functions)
            {
                // Make sure that if auto-invocation is specified, every enabled function can be found in the kernel.
                if (autoInvoke)
                {
                    if (!kernel!.Plugins.TryGetFunction(func.PluginName, func.FunctionName, out _))
                    {
                        throw new KernelException(
                            $"The specified {nameof(EnabledFunctions)} function {func.FullyQualifiedName} is not available in the kernel.");
                    }
                }

                // Add the function.
                request.AddFunction(func);
            }
        }
    }
}
