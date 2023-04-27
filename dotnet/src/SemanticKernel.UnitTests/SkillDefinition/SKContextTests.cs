﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Security;
using Microsoft.SemanticKernel.SkillDefinition;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.SkillDefinition;

public class SKContextTests
{
    private readonly Mock<IReadOnlySkillCollection> _skills;
    private readonly Mock<ILogger> _log;

    public SKContextTests()
    {
        this._skills = new Mock<IReadOnlySkillCollection>();
        this._log = new Mock<ILogger>();
    }

    [Fact]
    public void ItHasHelpersForContextVariables()
    {
        // Arrange
        var variables = new ContextVariables();
        var target = new SKContext(variables, skills: this._skills.Object, logger: this._log.Object);
        variables.Set("foo1", "bar1");

        // Act
        target["foo2"] = "bar2";
        target["INPUT"] = Guid.NewGuid().ToString("N");

        // Assert
        Assert.Equal("bar1", target["foo1"]);
        Assert.Equal("bar1", target.Variables["foo1"]);
        Assert.Equal("bar2", target["foo2"]);
        Assert.Equal("bar2", target.Variables["foo2"]);
        Assert.Equal(target["INPUT"], target.Result);
        Assert.Equal(target["INPUT"], target.ToString());
        Assert.Equal(target["INPUT"], target.Variables.Input);
        Assert.Equal(target["INPUT"], target.Variables.ToString());
    }

    [Fact]
    public async Task ItHasHelpersForSkillCollectionAsync()
    {
        // Arrange
        IDictionary<string, ISKFunction> skill = KernelBuilder.Create().ImportSkill(new Parrot(), "test");
        this._skills.Setup(x => x.GetFunction("func")).Returns(skill["say"]);
        var target = new SKContext(new ContextVariables(), NullMemory.Instance, this._skills.Object, this._log.Object);
        Assert.NotNull(target.Skills);

        // Act
        var say = target.Skills.GetFunction("func");
        SKContext result = await say.InvokeAsync("ciao");

        // Assert
        Assert.Equal("ciao", result.Result);
    }

    [Fact]
    public void ItCanUntrustAll()
    {
        // Arrange
        var variables = new ContextVariables();
        var target = new SKContext(variables);

        // Assert
        Assert.True(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, true);

        // Act
        target.UntrustAll();

        // Assert
        Assert.False(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, false);
    }

    [Fact]
    public void UpdateResultWithUntrustedContentMakesTheContextUntrusted()
    {
        // Arrange
        string newResult = Guid.NewGuid().ToString();
        string someOtherResult = Guid.NewGuid().ToString();
        var variables = new ContextVariables();
        var target = new SKContext(variables);

        // Assert
        Assert.Empty(target.Result);
        Assert.True(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, true);

        // Act
        // Update with new result as untrusted
        target.UpdateResult(newResult, false);

        // Assert
        Assert.Equal(newResult, target.Result);
        Assert.False(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, false);
    }

    [Fact]
    public void UpdateResultInAlreadyUntrustedContextKeepsTheContextUntrusted()
    {
        // Arrange
        string originalResult = Guid.NewGuid().ToString();
        string newResult = Guid.NewGuid().ToString();
        var variables = new ContextVariables(TrustAwareString.Untrusted(originalResult));
        var target = new SKContext(variables);

        // Assert
        Assert.Equal(originalResult, target.Result);
        Assert.False(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, false);

        // Act
        // Update with new result as trusted, although
        // the context should be kept untrusted because of the previous result
        target.UpdateResult(newResult, true);

        // Assert
        Assert.Equal(newResult, target.Result);
        // Should be kept false because the previous result it already false
        Assert.False(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, false);
    }

    [Fact]
    public void UpdateResultWithNullContentKeepsPreviousResult()
    {
        // Arrange
        string originalResult = Guid.NewGuid().ToString();
        var variables = new ContextVariables(TrustAwareString.Trusted(originalResult));
        var target = new SKContext(variables);

        // Assert
        Assert.Equal(originalResult, target.Result);
        Assert.True(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, true);

        // Act
        // Update with null result should keep previous value
        target.UpdateResult(null, false);

        // Assert
        Assert.Equal(originalResult, target.Result);
        Assert.False(target.IsTrusted);
        AssertIsInputTrusted(target.Variables, false);
    }

    private sealed class Parrot
    {
        [SKFunction("say something")]
        // ReSharper disable once UnusedMember.Local
        public string Say(string text)
        {
            return text;
        }
    }

    private static void AssertIsInputTrusted(ContextVariables variables, bool expectedIsTrusted)
    {
        // Assert isTrusted matches
        Assert.Equal(expectedIsTrusted, variables.Input.IsTrusted);
    }
}
