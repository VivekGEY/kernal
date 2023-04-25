﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Skills.Document;
using Microsoft.SemanticKernel.Skills.Document.FileSystem;
using Moq;
using Xunit;
using static Microsoft.SemanticKernel.Skills.Document.DocumentSkill;

namespace SemanticKernel.Skills.UnitTests.Document;

public class DocumentSkillTests
{
    private readonly SKContext _context = new(new ContextVariables(), NullMemory.Instance, null, NullLogger.Instance);

    [Fact]
    public async Task ReadTextAsyncSucceedsAsync()
    {
        // Arrange
        var expectedText = Guid.NewGuid().ToString();
        var anyFilePath = Guid.NewGuid().ToString();

        var fileSystemAdapterMock = new Mock<IFileSystemAdapter>();
        fileSystemAdapterMock
            .Setup(mock => mock.GetFileContentStreamAsync(It.Is<string>(filePath => filePath.Equals(anyFilePath, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);

        var documentAdapterMock = new Mock<IDocumentAdapter>();
        documentAdapterMock
            .Setup(mock => mock.ReadText(It.IsAny<Stream>()))
            .Returns(expectedText);

        var target = new DocumentSkill(documentAdapterMock.Object, fileSystemAdapterMock.Object);

        // Act
        string actual = await target.ReadTextAsync(anyFilePath, this._context);

        // Assert
        Assert.Equal(expectedText, actual);
        Assert.False(this._context.ErrorOccurred);
        fileSystemAdapterMock.VerifyAll();
        documentAdapterMock.VerifyAll();
    }

    [Fact]
    public async Task AppendTextAsyncFileExistsSucceedsAsync()
    {
        // Arrange
        var anyText = Guid.NewGuid().ToString();
        var anyFilePath = Guid.NewGuid().ToString();

        var fileSystemAdapterMock = new Mock<IFileSystemAdapter>();
        fileSystemAdapterMock
            .Setup(mock => mock.FileExistsAsync(It.Is<string>(filePath => filePath.Equals(anyFilePath, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        fileSystemAdapterMock
            .Setup(mock => mock.GetWriteableFileStreamAsync(It.Is<string>(filePath => filePath.Equals(anyFilePath, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);

        var documentAdapterMock = new Mock<IDocumentAdapter>();
        documentAdapterMock
            .Setup(mock => mock.AppendText(It.IsAny<Stream>(), It.Is<string>(text => text.Equals(anyText, StringComparison.Ordinal))));

        var target = new DocumentSkill(documentAdapterMock.Object, fileSystemAdapterMock.Object);

        this._context.Variables.Set(Parameters.FilePath, anyFilePath);

        // Act
        await target.AppendTextAsync(anyText, this._context);

        // Assert
        Assert.False(this._context.ErrorOccurred);
        fileSystemAdapterMock.VerifyAll();
        documentAdapterMock.VerifyAll();
    }

    [Fact]
    public async Task AppendTextAsyncFileDoesNotExistSucceedsAsync()
    {
        // Arrange
        var anyText = Guid.NewGuid().ToString();
        var anyFilePath = Guid.NewGuid().ToString();

        var fileSystemAdapterMock = new Mock<IFileSystemAdapter>();
        fileSystemAdapterMock
            .Setup(mock => mock.FileExistsAsync(It.Is<string>(filePath => filePath.Equals(anyFilePath, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        fileSystemAdapterMock
            .Setup(mock => mock.CreateFileAsync(It.Is<string>(filePath => filePath.Equals(anyFilePath, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stream.Null);

        var documentAdapterMock = new Mock<IDocumentAdapter>();
        documentAdapterMock
            .Setup(mock => mock.Initialize(It.IsAny<Stream>()));
        documentAdapterMock
            .Setup(mock => mock.AppendText(It.IsAny<Stream>(), It.Is<string>(text => text.Equals(anyText, StringComparison.Ordinal))));

        var target = new DocumentSkill(documentAdapterMock.Object, fileSystemAdapterMock.Object);

        this._context.Variables.Set(Parameters.FilePath, anyFilePath);

        // Act
        await target.AppendTextAsync(anyText, this._context);

        // Assert
        Assert.False(this._context.ErrorOccurred);
        fileSystemAdapterMock.VerifyAll();
        documentAdapterMock.VerifyAll();
    }

    [Fact]
    public async Task AppendTextAsyncNoFilePathFailsAsync()
    {
        // Arrange
        var anyText = Guid.NewGuid().ToString();

        var fileSystemAdapterMock = new Mock<IFileSystemAdapter>();
        var documentAdapterMock = new Mock<IDocumentAdapter>();

        var target = new DocumentSkill(documentAdapterMock.Object, fileSystemAdapterMock.Object);

        // Act
        await target.AppendTextAsync(anyText, this._context);

        // Assert
        Assert.True(this._context.ErrorOccurred);
        fileSystemAdapterMock.Verify(mock => mock.GetWriteableFileStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        documentAdapterMock.Verify(mock => mock.AppendText(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never());
    }
}
