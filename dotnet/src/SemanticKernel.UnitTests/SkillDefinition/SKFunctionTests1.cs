﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.SkillDefinition;

public sealed class SKFunctionTests1
{
    private readonly Mock<IPromptTemplate> _promptTemplate;

    public SKFunctionTests1()
    {
        this._promptTemplate = new Mock<IPromptTemplate>();
        this._promptTemplate.Setup(x => x.RenderAsync(It.IsAny<SKContext>())).ReturnsAsync("foo");
        this._promptTemplate.Setup(x => x.GetParameters()).Returns(new List<ParameterView>());
    }

    [Fact]
    public void ItAllowsToUpdateServiceSettings()
    {
        // Arrange
        var templateConfig = new PromptTemplateConfig();
        var functionConfig = new SemanticFunctionConfig(templateConfig, this._promptTemplate.Object);
        var skFunction = SKFunction.FromSemanticConfig("sk", "name", functionConfig);
        var settings = new JsonObject
        {
            ["temperature"] = 0.9,
            ["max_tokens"] = 2001,
        };

        // Act
        skFunction.ServiceSettings["temperature"] = 1.3;
        skFunction.ServiceSettings["max_tokens"] = 130;

        // Assert
        Assert.Equal(1.3, skFunction.ServiceSettings["temperature"]?.GetValue<double>());
        Assert.Equal(130, skFunction.ServiceSettings["max_tokens"]?.GetValue<int>());

        // Act
        skFunction.ServiceSettings["temperature"] = 0.7;

        // Assert
        Assert.Equal(0.7, skFunction.ServiceSettings["temperature"]?.GetValue<double>());

        // Act
        skFunction.SetAIConfiguration(settings);

        // Assert
        Assert.Equal(settings["temperature"], skFunction.ServiceSettings["temperature"]);
        Assert.Equal(settings["max_tokens"], skFunction.ServiceSettings["max_tokens"]);
    }
}
