﻿using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace SemanticKernel.Connectors.UnitTests.MultiConnector.TextCompletion;

public class ArithmeticComputingStreamingResult : ArithmeticStreamingResultBase
{
    private readonly string _prompt;
    private readonly ArithmeticEngine _engine;

    public ArithmeticComputingStreamingResult(string prompt, ArithmeticEngine engine, TimeSpan callTime) : base()
    {
        this._prompt = prompt;
        this._engine = engine;
    }

    protected async override Task<ModelResult> GenerateModelResult()
    {
        var result = this._engine.Run(this._prompt);
        return new ModelResult(result);
    }
}