﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Diagnostics.Metering;

namespace Microsoft.SemanticKernel.Planning.Sequential;

/// <summary>
/// Builder for <see cref="ISequentialPlanner"/>.
/// </summary>
public class SequentialPlannerBuilder
{
    /// <summary>
    /// Initialize a new instance of the <see cref="SequentialPlannerBuilder"/> class.
    /// </summary>
    /// <param name="kernel">The semantic kernel instance.</param>
    public SequentialPlannerBuilder(IKernel kernel)
    {
        this._kernel = kernel;
    }

    /// <summary>
    /// Build a new planner instance using the settings passed so far.
    /// </summary>
    /// <returns>Planner instance.</returns>
    public ISequentialPlanner Build()
    {
        ISequentialPlanner instance = new SequentialPlanner(this._kernel, this._config, this.prompt);

        instance = new InstrumentedSequentialPlanner(instance, this._logger, this._meter);

        return instance;
    }

    /// <summary>
    /// Add configuration to the planner to be built.
    /// </summary>
    /// <param name="config">Instance of <see cref="SequentialPlannerConfig"/> planner configuration.</param>
    /// <returns>Updated planner builder including the given configuration.</returns>
    public SequentialPlannerBuilder AddConfiguration(SequentialPlannerConfig config)
    {
        Verify.NotNull(config);
        this._config = config;
        return this;
    }

    /// <summary>
    /// Add prompt to the planner to be built.
    /// </summary>
    /// <param name="prompt">Prompt string.</param>
    /// <returns>Updated planner builder including the given prompt.</returns>
    public SequentialPlannerBuilder AddPrompt(string prompt)
    {
        Verify.NotNullOrWhiteSpace(prompt);
        this.prompt = prompt;
        return this;
    }

    /// <summary>
    /// Add logging to the planner to be built.
    /// </summary>
    /// <param name="logger">Instance of <see cref="ILogger"/> to be used for planner logging.</param>
    /// <returns>Updated planner builder with added logging.</returns>
    public SequentialPlannerBuilder AddLogging(ILogger logger)
    {
        Verify.NotNull(logger);
        this._logger = logger;
        return this;
    }

    /// <summary>
    /// Add metering to the planner to be built.
    /// </summary>
    /// <param name="meter">Instance of <see cref="IMeter"/> to be used for planner metering.</param>
    /// <returns>Updated planner builder with added metering.</returns>
    public SequentialPlannerBuilder AddMetering(IMeter meter)
    {
        Verify.NotNull(meter);
        this._meter = meter;
        return this;
    }

    #region private ================================================================================

    private readonly IKernel _kernel;

    private SequentialPlannerConfig? _config = null;
    private string? prompt = null;
    private ILogger? _logger = NullLogger.Instance;
    private IMeter? _meter = NullMeter.Instance;

    #endregion
}
