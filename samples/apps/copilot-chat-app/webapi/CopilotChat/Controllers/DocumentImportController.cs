﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Text;
using SemanticKernel.Service.CopilotChat.Hubs;
using SemanticKernel.Service.CopilotChat.Models;
using SemanticKernel.Service.CopilotChat.Options;
using SemanticKernel.Service.CopilotChat.Storage;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using static SemanticKernel.Service.CopilotChat.Models.MemorySource;

namespace SemanticKernel.Service.CopilotChat.Controllers;

/// <summary>
/// Controller for importing documents.
/// </summary>
[ApiController]
public class DocumentImportController : ControllerBase
{
    /// <summary>
    /// Supported file types for import.
    /// </summary>
    private enum SupportedFileType
    {
        /// <summary>
        /// .txt
        /// </summary>
        Txt,

        /// <summary>
        /// .pdf
        /// </summary>
        Pdf,
    };

    private readonly ILogger<DocumentImportController> _logger;
    private readonly DocumentMemoryOptions _options;
    private readonly ChatSessionRepository _sessionRepository;
    private readonly ChatMemorySourceRepository _sourceRepository;
    private readonly ChatMessageRepository _messageRepository;
    private readonly ChatParticipantRepository _participantRepository;
    private const string GlobalDocumentUploadedClientCall = "GlobalDocumentUploaded";
    private const string ChatDocumentUploadedClientCall = "ChatDocumentUploaded";

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentImportController"/> class.
    /// </summary>
    public DocumentImportController(
        ILogger<DocumentImportController> logger,
        IOptions<DocumentMemoryOptions> documentMemoryOptions,
        ChatSessionRepository sessionRepository,
        ChatMemorySourceRepository sourceRepository,
        ChatMessageRepository messageRepository,
        ChatParticipantRepository participantRepository)
    {
        this._logger = logger;
        this._options = documentMemoryOptions.Value;
        this._sessionRepository = sessionRepository;
        this._sourceRepository = sourceRepository;
        this._messageRepository = messageRepository;
        this._participantRepository = participantRepository;
    }

    /// <summary>
    /// Service API for importing a document.
    /// </summary>
    [Authorize]
    [Route("importDocuments")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportDocumentsAsync(
        [FromServices] IKernel kernel,
        [FromServices] IHubContext<MessageRelayHub> messageRelayHubContext,
        [FromForm] DocumentImportForm documentImportForm)
    {
        try
        {
            await this.ValidateDocumentImportFormAsync(documentImportForm);
        }
        catch (ArgumentException ex)
        {
            return this.BadRequest(ex.Message);
        }

        this._logger.LogInformation("Importing {0} document(s)...", documentImportForm.FormFiles.Count);

        DocumentMessageContent documentMessageContent = new DocumentMessageContent();
        foreach (var formFile in documentImportForm.FormFiles)
        {
            var success = await this.ImportDocumentHelperAsync(kernel, formFile, documentImportForm);
            documentMessageContent.AddDocument(
                formFile.FileName,
                this.GetReadableByteString(formFile.Length),
                success);
        }

        // Broadcast the document uploaded event to other users.
        if (documentImportForm.DocumentScope == DocumentImportForm.DocumentScopes.Chat)
        {
            var chatMessage = await this.TryCreateDocumentUploadMessage(
                documentMessageContent,
                documentImportForm);
            if (chatMessage == null)
            {
                // It's Ok to have the message not created.
                return this.Ok();
            }

            var chatId = documentImportForm.ChatId.ToString();
            await messageRelayHubContext.Clients.Group(chatId)
                .SendAsync(ChatDocumentUploadedClientCall, chatMessage, chatId);

            return this.Ok(chatMessage);
        }

        await messageRelayHubContext.Clients.All.SendAsync(
            GlobalDocumentUploadedClientCall,
            documentMessageContent.ToFormattedStringNamesOnly(),
            documentImportForm.UserName
        );

        return this.Ok();
    }

    #region Private

    /// <summary>
    /// Validates the document import form.
    /// </summary>
    /// <param name="documentImportForm">The document import form.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Throws ArgumentException if validation fails.</exception>
    private async Task ValidateDocumentImportFormAsync(DocumentImportForm documentImportForm)
    {
        var formFiles = documentImportForm.FormFiles;

        if (formFiles.Count == 0)
        {
            throw new ArgumentException("No file was uploaded.");
        }
        else if (formFiles.Count > this._options.FileCountLimit)
        {
            throw new ArgumentException($"Too many files uploaded. Max file count is {this._options.FileCountLimit}.");
        }

        // Loop through the uploaded files and validate them before importing.
        foreach (var formFile in formFiles)
        {
            if (formFile.Length == 0)
            {
                throw new ArgumentException($"File {formFile.FileName} is empty.");
            }

            if (formFile.Length > this._options.FileSizeLimit)
            {
                throw new ArgumentException($"File {formFile.FileName} size exceeds the limit.");
            }

            // Make sure the file type is supported.
            var fileType = this.GetFileType(Path.GetFileName(formFile.FileName));
            switch (fileType)
            {
                case SupportedFileType.Txt:
                case SupportedFileType.Pdf:
                    break;
                default:
                    throw new ArgumentException($"Unsupported file type: {fileType}");
            }
        }

        // Make sure the user has access to the chat session if the document is uploaded to a chat session.
        if (documentImportForm.DocumentScope == DocumentImportForm.DocumentScopes.Chat
                && !(await this.UserHasAccessToChatAsync(documentImportForm.UserId, documentImportForm.ChatId)))
        {
            throw new ArgumentException("User does not have access to the chat session.");
        }
    }

    /// <summary>
    /// Import a single document.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="formFile">The form file.</param>
    /// <param name="documentImportForm">The document import form.</param>
    /// <returns>True if the file is imported successfully. False otherwise.</returns>
    private async Task<bool> ImportDocumentHelperAsync(IKernel kernel, IFormFile formFile, DocumentImportForm documentImportForm)
    {
        var fileType = this.GetFileType(Path.GetFileName(formFile.FileName));
        var documentContent = string.Empty;
        switch (fileType)
        {
            case SupportedFileType.Txt:
                documentContent = await this.ReadTxtFileAsync(formFile);
                break;
            case SupportedFileType.Pdf:
                documentContent = this.ReadPdfFile(formFile);
                break;
            default:
                // This should never happen. Validation should have already caught this.
                return false;
        }

        this._logger.LogInformation("Importing document {0}", formFile.FileName);

        // Create memory source
        var memorySource = await this.TryCreateAndUpsertMemorySourceAsync(formFile, documentImportForm);
        if (memorySource == null)
        {
            return false;
        }

        // Parse document content to memory
        try
        {
            await this.ParseDocumentContentToMemoryAsync(kernel, formFile.FileName, documentContent, documentImportForm, memorySource.Id);
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            await this._sourceRepository.DeleteAsync(memorySource);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Try to create and upsert a memory source.
    /// </summary>
    /// <param name="formFile">The file to be uploaded</param>
    /// <param name="documentImportForm">The document upload form that contains additional necessary info</param>
    /// <returns>A MemorySource object if successful, null otherwise</returns>
    private async Task<MemorySource?> TryCreateAndUpsertMemorySourceAsync(
        IFormFile formFile,
        DocumentImportForm documentImportForm)
    {
        var chatId = documentImportForm.ChatId.ToString();
        var userId = documentImportForm.UserId;
        var memorySource = new MemorySource(
            chatId,
            formFile.FileName,
            userId,
            MemorySourceType.File,
            formFile.Length,
            null);

        try
        {
            await this._sourceRepository.UpsertAsync(memorySource);
            return memorySource;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Try to create a chat message that represents document upload.
    /// </summary>
    /// <param name="chatId">The chat id</param>
    /// <param name="userName">The user id</param>
    /// <param name="documentMessageContent">The document message content</param>
    /// <param name="documentImportForm">The document upload form that contains additional necessary info</param>
    /// <returns>A ChatMessage object if successful, null otherwise</returns>
    private async Task<ChatMessage?> TryCreateDocumentUploadMessage(
        DocumentMessageContent documentMessageContent,
        DocumentImportForm documentImportForm)
    {
        var chatId = documentImportForm.ChatId.ToString();
        var userId = documentImportForm.UserId;
        var userName = documentImportForm.UserName;

        var chatMessage = ChatMessage.CreateDocumentMessage(
            userId,
            userName,
            chatId,
            documentMessageContent
        );

        try
        {
            await this._messageRepository.CreateAsync(chatMessage);
            return chatMessage;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a `long` byte count to a human-readable string.
    /// </summary>
    /// <param name="bytes">Byte count</param>
    /// <returns>Human-readable string of bytes</returns>
    private string GetReadableByteString(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblsBytes = bytes;
        for (i = 0; i < sizes.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblsBytes = bytes / 1024.0;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.#}{1}", dblsBytes, sizes[i]);
    }

    /// <summary>
    /// Get the file type from the file extension.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <returns>A SupportedFileType.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private SupportedFileType GetFileType(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return extension switch
        {
            ".txt" => SupportedFileType.Txt,
            ".pdf" => SupportedFileType.Pdf,
            _ => throw new ArgumentOutOfRangeException($"Unsupported file type: {extension}"),
        };
    }

    /// <summary>
    /// Read the content of a text file.
    /// </summary>
    /// <param name="file">An IFormFile object.</param>
    /// <returns>A string of the content of the file.</returns>
    private async Task<string> ReadTxtFileAsync(IFormFile file)
    {
        using var streamReader = new StreamReader(file.OpenReadStream());
        return await streamReader.ReadToEndAsync();
    }

    /// <summary>
    /// Read the content of a PDF file, ignoring images.
    /// </summary>
    /// <param name="file">An IFormFile object.</param>
    /// <returns>A string of the content of the file.</returns>
    private string ReadPdfFile(IFormFile file)
    {
        var fileContent = string.Empty;

        using var pdfDocument = PdfDocument.Open(file.OpenReadStream());
        foreach (var page in pdfDocument.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            fileContent += text;
        }

        return fileContent;
    }

    /// <summary>
    /// Parse the content of the document to memory.
    /// </summary>
    /// <param name="kernel">The kernel instance from the service</param>
    /// <param name="documentName">The name of the uploaded document</param>
    /// <param name="content">The file content read from the uploaded document</param>
    /// <param name="documentImportForm">The document upload form that contains additional necessary info</param>
    /// <param name="memorySourceId">The ID of the MemorySource that the document content is linked to</param>
    private async Task ParseDocumentContentToMemoryAsync(
        IKernel kernel,
        string documentName,
        string content,
        DocumentImportForm documentImportForm,
        string memorySourceId)
    {
        var targetCollectionName = documentImportForm.DocumentScope == DocumentImportForm.DocumentScopes.Global
            ? this._options.GlobalDocumentCollectionName
            : this._options.ChatDocumentCollectionNamePrefix + documentImportForm.ChatId;

        // Split the document into lines of text and then combine them into paragraphs.
        // Note that this is only one of many strategies to chunk documents. Feel free to experiment with other strategies.
        var lines = TextChunker.SplitPlainTextLines(content, this._options.DocumentLineSplitMaxTokens);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, this._options.DocumentParagraphSplitMaxLines);

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            await kernel.Memory.SaveInformationAsync(
                collection: targetCollectionName,
                text: paragraph,
                id: $"{memorySourceId}-{i}",
                description: $"Document: {documentName}");
        }

        this._logger.LogInformation(
            "Parsed {0} paragraphs from local file {1}",
            paragraphs.Count,
            documentName
        );
    }

    /// <summary>
    /// Check if the user has access to the chat session.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="chatId">The chat session ID.</param>
    /// <returns>A boolean indicating whether the user has access to the chat session.</returns>
    private async Task<bool> UserHasAccessToChatAsync(string userId, Guid chatId)
    {
        return await this._participantRepository.IsUserInChatAsync(userId, chatId.ToString());
    }

    #endregion
}
