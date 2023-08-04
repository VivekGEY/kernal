﻿namespace SemanticKernel.Connectors.UnitTests.MultiConnector.TextCompletion;

public class CallRequestCostCreditor
{
    private decimal _ongoingCost;

    public decimal OngoingCost
    {
        get => this._ongoingCost;
    }

    public void Reset()
    {
        this._ongoingCost = 0;
    }

    public void Credit(decimal cost)
    {
        this._ongoingCost += cost;
    }
}