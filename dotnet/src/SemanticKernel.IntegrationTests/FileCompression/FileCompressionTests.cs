﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Skills.FileCompression;
using Microsoft.SemanticKernel.Skills.Web;
using SemanticKernel.IntegrationTests;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.FileCompression;

public class FileCompressionTests : IDisposable
{
    public FileCompressionTests(ITestOutputHelper output)
    {
        this._logger = new XunitLogger<Kernel>(output);
        this._output = output;

        this._testOutputHelper = new RedirectOutput(output);
        Console.SetOut(this._testOutputHelper);
    }

    [Fact]
    public async Task ZipFileCompressionAndDecompressionTestAsync()
    {
        // Arrange - Create objects
        IKernel kernel = Kernel.Builder.WithLogger(this._logger).Build();
        using XunitLogger<WebFileDownloadSkill> skillLogger = new(this._output);
        var zipCompressor = new ZipFileCompressor();
        var skill = new FileCompressionSkill(zipCompressor);
        var fileCompression = kernel.ImportSkill(skill, "FileCompression");

        // Arrange - Create file to compress
        string tempPath = Path.GetTempPath();
        string tempFileName = Path.GetRandomFileName();
        var sourceFilePath = Path.Join(tempPath, tempFileName);
        await File.WriteAllTextAsync(sourceFilePath, new string('*', 100));

        var destinationFilePath = sourceFilePath + ".zip";
        var contextVariables = new ContextVariables(sourceFilePath);
        contextVariables.Set(FileCompressionSkill.Parameters.DestinationFilePath, destinationFilePath);

        // Act - Compress file and decompress it
        await kernel.RunAsync(contextVariables, fileCompression["CompressFileAsync"]);
        string uncompressedFilePath = sourceFilePath + ".original";
        File.Move(sourceFilePath, uncompressedFilePath);
        contextVariables = new ContextVariables(destinationFilePath);
        contextVariables.Set(FileCompressionSkill.Parameters.DestinationDirectoryPath, tempPath);
        await kernel.RunAsync(contextVariables, fileCompression["DecompressFileAsync"]);

        // Assert
        string uncompressedFileContents = await File.ReadAllTextAsync(uncompressedFilePath);
        string decompressedFilePath = sourceFilePath;
        string decompressedFileContents = await File.ReadAllTextAsync(uncompressedFilePath);
        Assert.Equal(uncompressedFileContents, decompressedFileContents);

        // Clean up
        File.Delete(destinationFilePath); // Zip file
        File.Delete(uncompressedFilePath);
        File.Delete(decompressedFilePath);
    }

    [Fact]
    public async Task ZipDirectoryCompressionAndDecompressionTestAsync()
    {
        // Arrange - Create objects
        const string File1 = "file1.txt";
        const string File2 = "file2.txt";
        IKernel kernel = Kernel.Builder.WithLogger(this._logger).Build();
        using XunitLogger<WebFileDownloadSkill> skillLogger = new(this._output);
        var zipCompressor = new ZipFileCompressor();
        var skill = new FileCompressionSkill(zipCompressor);
        var fileCompression = kernel.ImportSkill(skill, "FileCompression");

        // Arrange - Create a folder with 2 files to compress
        string tempPath = Path.GetTempPath();
        string tempSubDirectoryName = Path.GetRandomFileName();
        string directoryToCompress = Path.Join(tempPath, tempSubDirectoryName);
        Directory.CreateDirectory(directoryToCompress);
        var sourceFilePath1 = Path.Join(directoryToCompress, File1);
        await File.WriteAllTextAsync(sourceFilePath1, new string('*', 100));
        var sourceFilePath2 = Path.Join(directoryToCompress, File2);
        await File.WriteAllTextAsync(sourceFilePath2, new string('*', 200));
        var destinationFilePath = Path.Join(tempPath, tempSubDirectoryName + ".zip");

        var contextVariables = new ContextVariables(directoryToCompress);
        contextVariables.Set(FileCompressionSkill.Parameters.DestinationFilePath, destinationFilePath);

        // Act - Compress folder and decompress it
        await kernel.RunAsync(contextVariables, fileCompression["CompressDirectoryAsync"]);
        string decompressedFilesDirectory = Path.Join(tempPath, tempSubDirectoryName + "decompressed");
        contextVariables = new ContextVariables(destinationFilePath);
        contextVariables.Set(FileCompressionSkill.Parameters.DestinationDirectoryPath, decompressedFilesDirectory);
        await kernel.RunAsync(contextVariables, fileCompression["DecompressFileAsync"]);

        // Assert
        string uncompressedFile1Contents = await File.ReadAllTextAsync(sourceFilePath1);
        string uncompressedFile2Contents = await File.ReadAllTextAsync(sourceFilePath2);
        string decompressedFile1Contents = await File.ReadAllTextAsync(Path.Join(decompressedFilesDirectory, File1));
        string decompressedFile2Contents = await File.ReadAllTextAsync(Path.Join(decompressedFilesDirectory, File2));
        Assert.Equal(uncompressedFile1Contents, decompressedFile1Contents);
        Assert.Equal(uncompressedFile2Contents, decompressedFile2Contents);

        // Cleanup
        File.Delete(destinationFilePath); // Zip file
        Directory.Delete(directoryToCompress, recursive: true);
        Directory.Delete(decompressedFilesDirectory, recursive: true);
    }

    private readonly XunitLogger<Kernel> _logger;
    private readonly ITestOutputHelper _output;
    private readonly RedirectOutput _testOutputHelper;

    /// <summary>
    /// Implementation of IDisposable.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Code that does the actual disposal of resources.
    /// </summary>
    /// <param name="disposing">Dispose of resources only if this is true.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._logger.Dispose();
            this._testOutputHelper.Dispose();
        }
    }
}
