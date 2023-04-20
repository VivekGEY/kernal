﻿// Copyright (c) Microsoft. All rights reserved.

using System.CommandLine;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;

namespace SemanticKernel.Service.DocumentUploadApp;

/// <summary>
/// This console app imports a file to the CopilotChat WebAPI's document memory store.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        var config = Config.GetConfig();
        if (!Config.Validate(config))
        {
            Console.WriteLine("Error: Failed to read appsettings.json.");
            return;
        }

        var fileOption = new Option<FileInfo>(
            name: "--file",
            description: "The file to upload for injection."
        )
        { IsRequired = true };

        var userCollectionOption = new Option<bool>(
            name: "--user-collection",
            description: "Save the extracted context to an isolated user collection.",
            getDefaultValue: () => false
        );

        var rootCommand = new RootCommand(
            "This console app imports a file to the CopilotChat WebAPI's document memory store."
        )
        {
            fileOption, userCollectionOption
        };

        rootCommand.SetHandler(async (file, userCollection) =>
            {
                await UploadFileAsync(file, config!, userCollection);
            },
            fileOption, userCollectionOption
        );

        rootCommand.Invoke(args);
    }

    /// <summary>
    /// Acquires a user unique ID from Azure AD.
    /// </summary>
    private static async Task<string?> AcquireUserIdAsync(Config config)
    {
        Console.WriteLine("Requesting User Account ID...");

        string[] scopes = { "User.Read" };
        try
        {
            var app = PublicClientApplicationBuilder.Create(config.ClientId)
                .WithRedirectUri(config.RedirectUri)
                .Build();
            var result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
            var accounts = await app.GetAccountsAsync();
            if (!accounts.Any())
            {
                Console.WriteLine("Error: No accounts found");
                return null;
            }
            return accounts.First().HomeAccountId.Identifier;
        }
        catch (Exception ex) when (ex is MsalServiceException || ex is MsalClientException)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Conditionally uploads a file to the Document Store for parsing.
    /// </summary>
    /// <param name="file">The file to upload for injection.</param>
    /// <param name="config">Configuration.</param>
    /// <param name="toUserCollection">Save the extracted context to an isolated user collection.</param>
    private static async Task UploadFileAsync(FileInfo file, Config config, bool toUserCollection)
    {
        if (!file.Exists)
        {
            Console.WriteLine($"File {file.FullName} does not exist.");
            return;
        }

        if (toUserCollection)
        {
            var userId = await AcquireUserIdAsync(config);
            if (userId != null)
            {
                Console.WriteLine("Uploading and parsing file to user collection...");
                await UploadFileAsync(file, userId, config);
            }
        }
        else
        {
            Console.WriteLine("Uploading and parsing file to global collection...");
            await UploadFileAsync(file, string.Empty, config);
        }
    }

    /// <summary>
    /// Invokes the DocumentQuerySkill to upload a file to the Document Store for parsing.
    /// </summary>
    /// <param name="file">The file to upload for injection.</param>
    /// <param name="userId">The user unique ID. If empty, the file will be injected to a global collection that is available to all users.</param>
    /// <param name="config">Configuration.</param>
    private static async Task UploadFileAsync(
        FileInfo file, string userId, Config config)
    {
        string skillName = "DocumentQuerySkill";
        string functionName = "ParseLocalFile";
        string commandPath = $"skills/{skillName}/functions/{functionName}/invoke";

        using StringContent jsonContent = new(
            JsonSerializer.Serialize(
                new
                {
                    input = file.FullName,
                    Variables = new List<KeyValuePair<string, string>> {
                        new KeyValuePair<string, string>("userId", userId)
                    }
                }
            ),
            Encoding.UTF8,
            "application/json"
        );

        // Create a HttpClient instance and set the timeout to infinite since
        // large documents will take a while to parse.
        using HttpClientHandler clientHandler = new()
        {
            CheckCertificateRevocationList = true
        };
        using HttpClient httpClient = new(clientHandler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        try
        {
            using HttpResponseMessage response = await httpClient.PostAsync(
                new Uri(new Uri(config.ServiceUri), commandPath),
                jsonContent
            );
            Console.WriteLine("Uploading and parsing successful.");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
