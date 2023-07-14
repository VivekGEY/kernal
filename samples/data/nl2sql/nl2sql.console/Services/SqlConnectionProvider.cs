﻿// Copyright (c) Microsoft. All rights reserved.
namespace SemanticKernel.Data.Nl2Sql.Services;

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SemanticKernel.Data.Nl2Sql.Exceptions;

internal sealed class SqlConnectionProvider
{
    private readonly IConfiguration configuration;

    public static Func<IServiceProvider, SqlConnectionProvider> Create(IConfiguration configuration)
    {
        return CreateProvider;

        SqlConnectionProvider CreateProvider(IServiceProvider provider)
        {
            return new SqlConnectionProvider(configuration);
        }
    }

    private SqlConnectionProvider(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    /// <summary>
    /// Factory method for producing a live SQL connection instance.
    /// </summary>
    /// <param name="schemaName">The schema name (which should match a corresponding connectionstring setting).</param>
    /// <returns>A <see cref="SqlConnection"/> instance in the "Open" state.</returns>
    /// <remarks>
    /// Connection pooling enabled by default makes re-establishing connections
    /// relatively efficient.
    /// </remarks>
    public async Task<SqlConnection> ConnectAsync(string schemaName)
    {
        var connectionString =
            this.configuration.GetConnectionString(schemaName) ??
            throw new InvalidConfigurationException($"Missing configuration for connection-string: {schemaName}");

        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }
}
