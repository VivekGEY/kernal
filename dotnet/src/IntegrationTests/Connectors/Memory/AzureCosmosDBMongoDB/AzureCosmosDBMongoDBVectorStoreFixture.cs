﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Data;
using MongoDB.Driver;
using Xunit;

namespace SemanticKernel.IntegrationTests.Connectors.Memory.AzureCosmosDBMongoDB;

public class AzureCosmosDBMongoDBVectorStoreFixture : IAsyncLifetime
{
    private readonly List<string> _testCollections = ["sk-test-hotels", "sk-test-contacts", "sk-test-addresses"];

    /// <summary>Main test collection for tests.</summary>
    public string TestCollection => this._testCollections[0];

    /// <summary><see cref="IMongoDatabase"/> that can be used to manage the collections in Azure CosmosDB MongoDB.</summary>
    public IMongoDatabase MongoDatabase { get; }

    /// <summary>Gets the manually created vector store record definition for our test model.</summary>
    public VectorStoreRecordDefinition HotelVectorStoreRecordDefinition { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCosmosDBMongoDBVectorStoreFixture"/> class.
    /// </summary>
    public AzureCosmosDBMongoDBVectorStoreFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(
                path: "testsettings.development.json",
                optional: false,
                reloadOnChange: true
            )
            .AddEnvironmentVariables()
            .Build();

        var connectionString = GetConnectionString(configuration);
        var client = new MongoClient(connectionString);

        this.MongoDatabase = client.GetDatabase("test");

        this.HotelVectorStoreRecordDefinition = new()
        {
            Properties =
            [
                new VectorStoreRecordKeyProperty("HotelId"),
                new VectorStoreRecordDataProperty("HotelName") { IsFilterable = true, PropertyType = typeof(string) },
                new VectorStoreRecordDataProperty("HotelCode") { IsFilterable = true, PropertyType = typeof(int) },
                new VectorStoreRecordDataProperty("ParkingIncluded") { IsFilterable = true, PropertyType = typeof(bool), StoragePropertyName = "parking_is_included" },
                new VectorStoreRecordDataProperty("HotelRating") { IsFilterable = true, PropertyType = typeof(float) },
                new VectorStoreRecordDataProperty("Tags"),
                new VectorStoreRecordDataProperty("Description"),
                new VectorStoreRecordVectorProperty("DescriptionEmbedding") { Dimensions = 4, IndexKind = "vector-ivf", DistanceFunction = "COS" }
            ]
        };
    }

    public async Task InitializeAsync()
    {
        foreach (var collection in this._testCollections)
        {
            await this.MongoDatabase.CreateCollectionAsync(collection);
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var collection in this._testCollections)
        {
            await this.MongoDatabase.DropCollectionAsync(collection);
        }
    }

#pragma warning disable CS8618
    public record AzureCosmosDBMongoDBHotel()
    {
        /// <summary>The key of the record.</summary>
        [VectorStoreRecordKey]
        public string HotelId { get; init; }

        /// <summary>A string metadata field.</summary>
        [VectorStoreRecordData(IsFilterable = true)]
        public string? HotelName { get; set; }

        /// <summary>An int metadata field.</summary>
        [VectorStoreRecordData(IsFilterable = true)]
        public int HotelCode { get; set; }

        /// <summary>A  float metadata field.</summary>
        [VectorStoreRecordData(IsFilterable = true)]
        public float? HotelRating { get; set; }

        /// <summary>A bool metadata field.</summary>
        [VectorStoreRecordData(IsFilterable = true, StoragePropertyName = "parking_is_included")]
        public bool ParkingIncluded { get; set; }

        [VectorStoreRecordData]
        public List<string> Tags { get; set; } = [];

        /// <summary>A data field.</summary>
        [VectorStoreRecordData(HasEmbedding = true, EmbeddingPropertyName = "DescriptionEmbedding")]
        public string Description { get; set; }

        /// <summary>A vector field.</summary>
        [VectorStoreRecordVector(Dimensions: 4, IndexKind: "vector-ivf", DistanceFunction: "COS")]
        public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
    }
#pragma warning restore CS8618

    #region private

    private static string GetConnectionString(IConfigurationRoot configuration)
    {
        var settingValue = configuration["AzureCosmosDBMongoDB:ConnectionString"];
        if (string.IsNullOrWhiteSpace(settingValue))
        {
            throw new ArgumentNullException($"{settingValue} string is not configured");
        }

        return settingValue;
    }

    #endregion
}
