﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCosmosDb;
using Microsoft.SemanticKernel.Memory;

namespace SemanticKernel.Connectors.UnitTests.Memory.CosmosDB;


//Unit tests for cosmosDB connector, disable by default as it requires cosmosDB emulator at localhost
//public class CosmosDBMemoryStoreTest : MemoryStorageTestBase
//{
//    //Unit test requires local cosmosDB emulator
//    //https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator?tabs=ssl-netstd21
//    private const string COSMOS_EMULATOR = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

//    protected override async Task WithStorageAsync(Func<IMemoryStore, Task> factAsync)
//    {
//#pragma warning disable CA2000 // CosmosClient is held by CosmosStore, should not be disposed in this call
//        CosmosClient client = new CosmosClient(COSMOS_EMULATOR);
//#pragma warning restore CA2000 // Dispose objects before losing scope
//        var db = await CosmosMemoryStore.CreateAsync(client, "TestDB");
//        await factAsync(db).ConfigureAwait(false);
//    }

//}
