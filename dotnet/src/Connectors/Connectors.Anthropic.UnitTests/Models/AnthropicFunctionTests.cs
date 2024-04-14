﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Anthropic;
using Xunit;

namespace SemanticKernel.Connectors.Anthropic.UnitTests.Models;

public sealed class AnthropicFunctionTests
{
    [Theory]
    [InlineData(null, null, "", "")]
    [InlineData("name", "description", "name", "description")]
    public void ItInitializesClaudeFunctionParameterCorrectly(string? name, string? description, string expectedName, string expectedDescription)
    {
        // Arrange & Act
        var schema = KernelJsonSchema.Parse("""{"type": "object" }""");
        var functionParameter = new ClaudeFunctionParameter(name, description, true, typeof(string), schema);

        // Assert
        Assert.Equal(expectedName, functionParameter.Name);
        Assert.Equal(expectedDescription, functionParameter.Description);
        Assert.True(functionParameter.IsRequired);
        Assert.Equal(typeof(string), functionParameter.ParameterType);
        Assert.Same(schema, functionParameter.Schema);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("description", "description")]
    public void ItInitializesClaudeFunctionReturnParameterCorrectly(string? description, string expectedDescription)
    {
        // Arrange & Act
        var schema = KernelJsonSchema.Parse("""{"type": "object" }""");
        var functionParameter = new ClaudeFunctionReturnParameter(description, typeof(string), schema);

        // Assert
        Assert.Equal(expectedDescription, functionParameter.Description);
        Assert.Equal(typeof(string), functionParameter.ParameterType);
        Assert.Same(schema, functionParameter.Schema);
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionWithNoPluginName()
    {
        // Arrange
        AnthropicFunction sut = KernelFunctionFactory.CreateFromMethod(
            () => { }, "myfunc", "This is a description of the function.").Metadata.ToClaudeFunction();

        // Act
        var result = sut.ToFunctionDeclaration();

        // Assert
        Assert.Equal(sut.FunctionName, result.Name);
        Assert.Equal(sut.Description, result.Description);
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionWithNullParameters()
    {
        // Arrange
        AnthropicFunction sut = new("plugin", "function", "description", null, null);

        // Act
        var result = sut.ToFunctionDeclaration();

        // Assert
        Assert.Null(result.Parameters);
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionWithPluginName()
    {
        // Arrange
        AnthropicFunction sut = KernelPluginFactory.CreateFromFunctions("myplugin", new[]
        {
            KernelFunctionFactory.CreateFromMethod(() => { }, "myfunc", "This is a description of the function.")
        }).GetFunctionsMetadata()[0].ToClaudeFunction();

        // Act
        var result = sut.ToFunctionDeclaration();

        // Assert
        Assert.Equal($"myplugin{AnthropicFunction.NameSeparator}myfunc", result.Name);
        Assert.Equal(sut.Description, result.Description);
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionsWithParameterTypesAndReturnParameterType()
    {
        string expectedParameterSchema = """
                                         {   "type": "object",
                                         "required": ["param1", "param2"],
                                         "properties": {
                                         "param1": { "type": "string", "description": "String param 1" },
                                         "param2": { "type": "integer", "description": "Int param 2" }   } }
                                         """;

        KernelPlugin plugin = KernelPluginFactory.CreateFromFunctions("Tests", new[]
        {
            KernelFunctionFactory.CreateFromMethod(
                [return: Description("My test Result")]
                ([Description("String param 1")] string param1, [Description("Int param 2")] int param2) => "",
                "TestFunction",
                "My test function")
        });

        AnthropicFunction sut = plugin.GetFunctionsMetadata()[0].ToClaudeFunction();

        var functionDefinition = sut.ToFunctionDeclaration();

        Assert.NotNull(functionDefinition);
        Assert.Equal($"Tests{AnthropicFunction.NameSeparator}TestFunction", functionDefinition.Name);
        Assert.Equal("My test function", functionDefinition.Description);
        Assert.Equal(JsonSerializer.Serialize(KernelJsonSchema.Parse(expectedParameterSchema)),
            JsonSerializer.Serialize(functionDefinition.Parameters));
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionsWithParameterTypesAndNoReturnParameterType()
    {
        string expectedParameterSchema = """
                                         {   "type": "object",
                                         "required": ["param1", "param2"],
                                         "properties": {
                                         "param1": { "type": "string", "description": "String param 1" },
                                         "param2": { "type": "integer", "description": "Int param 2" }   } }
                                         """;

        KernelPlugin plugin = KernelPluginFactory.CreateFromFunctions("Tests", new[]
        {
            KernelFunctionFactory.CreateFromMethod(
                [return: Description("My test Result")]
                ([Description("String param 1")] string param1, [Description("Int param 2")] int param2) => { },
                "TestFunction",
                "My test function")
        });

        AnthropicFunction sut = plugin.GetFunctionsMetadata()[0].ToClaudeFunction();

        var functionDefinition = sut.ToFunctionDeclaration();

        Assert.NotNull(functionDefinition);
        Assert.Equal($"Tests{AnthropicFunction.NameSeparator}TestFunction", functionDefinition.Name);
        Assert.Equal("My test function", functionDefinition.Description);
        Assert.Equal(JsonSerializer.Serialize(KernelJsonSchema.Parse(expectedParameterSchema)),
            JsonSerializer.Serialize(functionDefinition.Parameters));
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionsWithNoParameterTypes()
    {
        // Arrange
        AnthropicFunction f = KernelFunctionFactory.CreateFromMethod(
            () => { },
            parameters: new[] { new KernelParameterMetadata("param1") }).Metadata.ToClaudeFunction();

        // Act
        var result = f.ToFunctionDeclaration();

        // Assert
        Assert.Equal(
            """{"type":"object","required":[],"properties":{"param1":{"type":"string"}}}""",
            JsonSerializer.Serialize(result.Parameters));
    }

    [Fact]
    public void ItCanConvertToFunctionDefinitionsWithNoParameterTypesButWithDescriptions()
    {
        // Arrange
        AnthropicFunction f = KernelFunctionFactory.CreateFromMethod(
            () => { },
            parameters: new[] { new KernelParameterMetadata("param1") { Description = "something neat" } }).Metadata.ToClaudeFunction();

        // Act
        var result = f.ToFunctionDeclaration();

        // Assert
        Assert.Equal(
            """{"type":"object","required":[],"properties":{"param1":{"type":"string","description":"something neat"}}}""",
            JsonSerializer.Serialize(result.Parameters));
    }
}
