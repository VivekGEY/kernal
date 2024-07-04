﻿// Copyright (c) Microsoft. All rights reserved.

/* 
Phase 05
- Ignoring the specific Purposes not implemented by current FileService.
*/

using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Files;

using OAIFilePurpose = OpenAI.Files.OpenAIFilePurpose;
using SKFilePurpose = Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIFilePurpose;

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with OpenAI services.
/// </summary>
internal partial class ClientCore
{
    /// <summary>
    /// Uploads a file to OpenAI.
    /// </summary>
    /// <param name="fileName">File name</param>
    /// <param name="fileContent">File content</param>
    /// <param name="purpose">Purpose of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Uploaded file information</returns>
    internal async Task<OpenAIFileReference> UploadFileAsync(
        string fileName,
        Stream fileContent,
        SKFilePurpose purpose,
        CancellationToken cancellationToken)
    {
        ClientResult<OpenAIFileInfo> response = await RunRequestAsync(() => this.Client.GetFileClient().UploadFileAsync(fileContent, fileName, ConvertToOpenAIFilePurpose(purpose), cancellationToken)).ConfigureAwait(false);
        return ConvertToFileReference(response.Value);
    }

    /// <summary>
    /// Delete a previously uploaded file.
    /// </summary>
    /// <param name="fileId">The uploaded file identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    internal async Task DeleteFileAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        await RunRequestAsync(() => this.Client.GetFileClient().DeleteFileAsync(fileId, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieve metadata for a previously uploaded file.
    /// </summary>
    /// <param name="fileId">The uploaded file identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The metadata associated with the specified file identifier.</returns>
    internal async Task<OpenAIFileReference> GetFileAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        ClientResult<OpenAIFileInfo> response = await RunRequestAsync(() => this.Client.GetFileClient().GetFileAsync(fileId, cancellationToken)).ConfigureAwait(false);
        return ConvertToFileReference(response.Value);
    }

    /// <summary>
    /// Retrieve metadata for all previously uploaded files.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The metadata of all uploaded files.</returns>
    internal async Task<IEnumerable<OpenAIFileReference>> GetFilesAsync(CancellationToken cancellationToken)
    {
        ClientResult<OpenAIFileInfoCollection> response = await RunRequestAsync(() => this.Client.GetFileClient().GetFilesAsync(cancellationToken: cancellationToken)).ConfigureAwait(false);
        return response.Value.Select(ConvertToFileReference);
    }

    /// <summary>
    /// Retrieve the file content from a previously uploaded file.
    /// </summary>
    /// <param name="fileId">The uploaded file identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The file content as <see cref="BinaryContent"/></returns>
    /// <remarks>
    /// Files uploaded with <see cref="OpenAIFilePurpose.Assistants"/> do not support content retrieval.
    /// </remarks>
    internal async Task<byte[]> GetFileContentAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        ClientResult<BinaryData> response = await RunRequestAsync(() => this.Client.GetFileClient().DownloadFileAsync(fileId, cancellationToken)).ConfigureAwait(false);
        return response.Value.ToArray();
    }

    private static OpenAIFileReference ConvertToFileReference(OpenAIFileInfo fileInfo)
        => new()
        {
            Id = fileInfo.Id,
            CreatedTimestamp = fileInfo.CreatedAt.DateTime,
            FileName = fileInfo.Filename,
            SizeInBytes = (int)(fileInfo.SizeInBytes ?? 0),
            Purpose = ConvertToFilePurpose(fileInfo.Purpose),
        };

    private static FileUploadPurpose ConvertToOpenAIFilePurpose(SKFilePurpose purpose)
    {
        if (purpose == SKFilePurpose.Assistants) { return FileUploadPurpose.Assistants; }
        if (purpose == SKFilePurpose.FineTune) { return FileUploadPurpose.FineTune; }

        throw new KernelException($"Unknown {nameof(OpenAIFilePurpose)}: {purpose}.");
    }

    private static SKFilePurpose ConvertToFilePurpose(OAIFilePurpose purpose)
    {
        if (purpose == OAIFilePurpose.Assistants) { return SKFilePurpose.Assistants; }
        if (purpose == OAIFilePurpose.FineTune) { return SKFilePurpose.FineTune; }

        throw new KernelException($"Unknown {nameof(OpenAIFilePurpose)}: {purpose}.");
    }
}
