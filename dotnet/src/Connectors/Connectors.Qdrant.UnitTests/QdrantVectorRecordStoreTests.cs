﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using Moq;
using Qdrant.Client.Grpc;
using Xunit;

namespace Microsoft.SemanticKernel.Connectors.Qdrant.UnitTests;

/// <summary>
/// Contains tests for the <see cref="QdrantVectorRecordStore{TRecord}"/> class.
/// </summary>
public class QdrantVectorRecordStoreTests
{
    private const string TestCollectionName = "testcollection";
    private const ulong UlongTestRecordKey1 = 1;
    private const ulong UlongTestRecordKey2 = 2;
    private static readonly Guid s_guidTestRecordKey1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_guidTestRecordKey2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<MockableQdrantClient> _qdrantClientMock;

    private readonly CancellationToken _testCancellationToken = new(false);

    public QdrantVectorRecordStoreTests()
    {
        this._qdrantClientMock = new Mock<MockableQdrantClient>(MockBehavior.Strict);
    }

    [Theory]
    [MemberData(nameof(TestOptions))]
    public Task CanGetRecordWithVectorsAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, Type keyType)
    {
        if (keyType == typeof(ulong))
        {
            return this.CanGetRecordWithVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, UlongTestRecordKey1);
        }

        if (keyType == typeof(Guid))
        {
            return this.CanGetRecordWithVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, s_guidTestRecordKey1);
        }

        Assert.Fail("No valid key type provided.");
        return Task.CompletedTask;
    }

    private async Task CanGetRecordWithVectorsInternalAsync<TKey>(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, TKey testRecordKey)
    {
        var sut = this.CreateVectorRecordStore<TKey>(useDefinition, passCollectionToMethod, hasNamedVectors);

        // Arrange.
        var retrievedPoint = CreateRetrievedPoint(hasNamedVectors, testRecordKey);
        this.SetupRetrieveMock([retrievedPoint]);

        // Act.
        var actual = await sut.GetAsync(
            testRecordKey,
            new()
            {
                IncludeVectors = true,
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken);

        // Assert.
        this._qdrantClientMock
            .Verify(
                x => x.RetrieveAsync(
                    TestCollectionName,
                    It.Is<IReadOnlyList<PointId>>(x => x.Count == 1 && (testRecordKey!.GetType() == typeof(ulong) && x[0].Num == (testRecordKey as ulong?) || testRecordKey!.GetType() == typeof(Guid) && x[0].Uuid == (testRecordKey as Guid?).ToString())),
                    true,
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);

        Assert.NotNull(actual);
        Assert.Equal(testRecordKey, actual.Key);
        Assert.Equal("data 1", actual.Data);
        Assert.Equal(new float[] { 1, 2, 3, 4 }, actual.Vector!.Value.ToArray());
    }

    [Theory]
    [MemberData(nameof(TestOptions))]
    public Task CanGetRecordWithoutVectorsAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, Type keyType)
    {
        if (keyType == typeof(ulong))
        {
            return this.CanGetRecordWithoutVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, UlongTestRecordKey1);
        }

        if (keyType == typeof(Guid))
        {
            return this.CanGetRecordWithoutVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, s_guidTestRecordKey1);
        }

        Assert.Fail("No valid key type provided.");
        return Task.CompletedTask;
    }

    private async Task CanGetRecordWithoutVectorsInternalAsync<TKey>(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, TKey testRecordKey)
    {
        // Arrange.
        var sut = this.CreateVectorRecordStore<TKey>(useDefinition, passCollectionToMethod, hasNamedVectors);
        var retrievedPoint = CreateRetrievedPoint(hasNamedVectors, testRecordKey);
        this.SetupRetrieveMock([retrievedPoint]);

        // Act.
        var actual = await sut.GetAsync(
            testRecordKey,
            new()
            {
                IncludeVectors = false,
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken);

        // Assert.
        this._qdrantClientMock
            .Verify(
                x => x.RetrieveAsync(
                    TestCollectionName,
                    It.Is<IReadOnlyList<PointId>>(x => x.Count == 1 && (testRecordKey!.GetType() == typeof(ulong) && x[0].Num == (testRecordKey as ulong?) || testRecordKey!.GetType() == typeof(Guid) && x[0].Uuid == (testRecordKey as Guid?).ToString())),
                    true,
                    false,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);

        Assert.NotNull(actual);
        Assert.Equal(testRecordKey, actual.Key);
        Assert.Equal("data 1", actual.Data);
        Assert.Null(actual.Vector);
    }

    [Theory]
    [MemberData(nameof(TestOptions))]
    public Task CanGetManyRecordsWithVectorsAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, Type keyType)
    {
        if (keyType == typeof(ulong))
        {
            return this.CanGetManyRecordsWithVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, [UlongTestRecordKey1, UlongTestRecordKey2]);
        }

        if (keyType == typeof(Guid))
        {
            return this.CanGetManyRecordsWithVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, [s_guidTestRecordKey1, s_guidTestRecordKey2]);
        }

        Assert.Fail("No valid key type provided.");
        return Task.CompletedTask;
    }

    private async Task CanGetManyRecordsWithVectorsInternalAsync<TKey>(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, TKey[] testRecordKeys)
    {
        // Arrange.
        var sut = this.CreateVectorRecordStore<TKey>(useDefinition, passCollectionToMethod, hasNamedVectors);
        var retrievedPoint1 = CreateRetrievedPoint(hasNamedVectors, UlongTestRecordKey1);
        var retrievedPoint2 = CreateRetrievedPoint(hasNamedVectors, UlongTestRecordKey2);
        this.SetupRetrieveMock(testRecordKeys.Select(x => CreateRetrievedPoint(hasNamedVectors, x)).ToList());

        // Act.
        var actual = await sut.GetBatchAsync(
            testRecordKeys,
            new()
            {
                IncludeVectors = true,
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken).ToListAsync();

        // Assert.
        this._qdrantClientMock
            .Verify(
                x => x.RetrieveAsync(
                    TestCollectionName,
                    It.Is<IReadOnlyList<PointId>>(x =>
                        x.Count == 2 &&
                        (testRecordKeys[0]!.GetType() == typeof(ulong) && x[0].Num == (testRecordKeys[0] as ulong?) || testRecordKeys[0]!.GetType() == typeof(Guid) && x[0].Uuid == (testRecordKeys[0] as Guid?).ToString()) &&
                        (testRecordKeys[1]!.GetType() == typeof(ulong) && x[1].Num == (testRecordKeys[1] as ulong?) || testRecordKeys[1]!.GetType() == typeof(Guid) && x[1].Uuid == (testRecordKeys[1] as Guid?).ToString())),
                    true,
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);

        Assert.NotNull(actual);
        Assert.Equal(2, actual.Count);
        Assert.Equal(testRecordKeys[0], actual[0].Key);
        Assert.Equal(testRecordKeys[1], actual[1].Key);
    }

    [Fact]
    public async Task CanGetRecordWithCustomMapperAsync()
    {
        // Arrange.
        var retrievedPoint = CreateRetrievedPoint(true, UlongTestRecordKey1);
        this.SetupRetrieveMock([retrievedPoint]);

        // Arrange mapper mock from PointStruct to data model.
        var mapperMock = new Mock<IVectorStoreRecordMapper<SinglePropsModel<ulong>, PointStruct>>(MockBehavior.Strict);
        mapperMock.Setup(
            x => x.MapFromStorageToDataModel(
                It.IsAny<PointStruct>(),
                It.IsAny<StorageToDataModelMapperOptions>()))
            .Returns(CreateModel(UlongTestRecordKey1, true));

        // Arrange target with custom mapper.
        var sut = new QdrantVectorRecordStore<SinglePropsModel<ulong>>(
            this._qdrantClientMock.Object,
            new()
            {
                DefaultCollectionName = TestCollectionName,
                HasNamedVectors = true,
                MapperType = QdrantRecordMapperType.QdrantPointStructCustomMapper,
                PointStructCustomMapper = mapperMock.Object
            });

        // Act
        var actual = await sut.GetAsync(
            UlongTestRecordKey1,
            new() { IncludeVectors = true },
            this._testCancellationToken);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(UlongTestRecordKey1, actual.Key);
        Assert.Equal("data 1", actual.Data);
        Assert.Equal(new float[] { 1, 2, 3, 4 }, actual.Vector!.Value.ToArray());

        mapperMock
            .Verify(
                x => x.MapFromStorageToDataModel(
                    It.Is<PointStruct>(x => x.Id.Num == UlongTestRecordKey1),
                    It.Is<StorageToDataModelMapperOptions>(x => x.IncludeVectors)),
                Times.Once);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task CanDeleteUlongRecordAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors)
    {
        // Arrange
        var sut = this.CreateVectorRecordStore<ulong>(useDefinition, passCollectionToMethod, hasNamedVectors);
        this.SetupDeleteMocks();

        // Act
        await sut.DeleteAsync(
            UlongTestRecordKey1,
            new()
            {
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken);

        // Assert
        this._qdrantClientMock
            .Verify(
                x => x.DeleteAsync(
                    TestCollectionName,
                    It.Is<ulong>(x => x == UlongTestRecordKey1),
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task CanDeleteGuidRecordAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors)
    {
        // Arrange
        var sut = this.CreateVectorRecordStore<Guid>(useDefinition, passCollectionToMethod, hasNamedVectors);
        this.SetupDeleteMocks();

        // Act
        await sut.DeleteAsync(
            s_guidTestRecordKey1,
            new()
            {
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken);

        // Assert
        this._qdrantClientMock
            .Verify(
                x => x.DeleteAsync(
                    TestCollectionName,
                    It.Is<Guid>(x => x == s_guidTestRecordKey1),
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task CanDeleteManyUlongRecordsAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors)
    {
        // Arrange
        var sut = this.CreateVectorRecordStore<ulong>(useDefinition, passCollectionToMethod, hasNamedVectors);
        this.SetupDeleteMocks();

        // Act
        await sut.DeleteBatchAsync(
            [UlongTestRecordKey1, UlongTestRecordKey2],
            new()
            {
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken);

        // Assert
        this._qdrantClientMock
            .Verify(
                x => x.DeleteAsync(
                    TestCollectionName,
                    It.Is<IReadOnlyList<ulong>>(x => x.Count == 2 && x.Contains(UlongTestRecordKey1) && x.Contains(UlongTestRecordKey2)),
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task CanDeleteManyGuidRecordsAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors)
    {
        // Arrange
        var sut = this.CreateVectorRecordStore<Guid>(useDefinition, passCollectionToMethod, hasNamedVectors);
        this.SetupDeleteMocks();

        // Act
        await sut.DeleteBatchAsync(
            [s_guidTestRecordKey1, s_guidTestRecordKey2],
            new()
            {
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken);

        // Assert
        this._qdrantClientMock
            .Verify(
                x => x.DeleteAsync(
                    TestCollectionName,
                    It.Is<IReadOnlyList<Guid>>(x => x.Count == 2 && x.Contains(s_guidTestRecordKey1) && x.Contains(s_guidTestRecordKey2)),
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);
    }

    [Theory]
    [MemberData(nameof(TestOptions))]
    public Task CanUpsertRecordAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, Type keyType)
    {
        if (keyType == typeof(ulong))
        {
            return this.CanGetRecordWithoutVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, UlongTestRecordKey1);
        }

        if (keyType == typeof(Guid))
        {
            return this.CanGetRecordWithoutVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, s_guidTestRecordKey1);
        }

        Assert.Fail("No valid key type provided.");
        return Task.CompletedTask;
    }

    private async Task CanUpsertRecordInternalAsync<TKey>(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, TKey testRecordKey)
    {
        // Arrange
        var sut = this.CreateVectorRecordStore<TKey>(useDefinition, passCollectionToMethod, hasNamedVectors);
        this.SetupUpsertMock();

        // Act
        await sut.UpsertAsync(
            CreateModel(testRecordKey, true),
            new()
            {
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken);

        // Assert
        this._qdrantClientMock
            .Verify(
                x => x.UpsertAsync(
                    TestCollectionName,
                    It.Is<IReadOnlyList<PointStruct>>(x => x.Count == 1 && (testRecordKey!.GetType() == typeof(ulong) && x[0].Id.Num == (testRecordKey as ulong?) || testRecordKey!.GetType() == typeof(Guid) && x[0].Id.Uuid == (testRecordKey as Guid?).ToString())),
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);
    }

    [Theory]
    [MemberData(nameof(TestOptions))]
    public Task CanUpsertManyRecordsAsync(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, Type keyType)
    {
        if (keyType == typeof(ulong))
        {
            return this.CanGetRecordWithoutVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, UlongTestRecordKey1);
        }

        if (keyType == typeof(Guid))
        {
            return this.CanGetRecordWithoutVectorsInternalAsync(useDefinition, passCollectionToMethod, hasNamedVectors, s_guidTestRecordKey1);
        }

        Assert.Fail("No valid key type provided.");
        return Task.CompletedTask;
    }

    private async Task CanUpsertManyRecordsInternalAsync<TKey>(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors, TKey[] testRecordKeys)
    {
        // Arrange
        var sut = this.CreateVectorRecordStore<TKey>(useDefinition, passCollectionToMethod, hasNamedVectors);
        this.SetupUpsertMock();

        var models = testRecordKeys.Select(x => CreateModel(x, true));

        // Act
        var actual = await sut.UpsertBatchAsync(
            models,
            new()
            {
                CollectionName = passCollectionToMethod ? TestCollectionName : null
            },
            this._testCancellationToken).ToListAsync();

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(2, actual.Count);
        Assert.Equal(testRecordKeys[0], actual[0]);
        Assert.Equal(testRecordKeys[1], actual[1]);

        this._qdrantClientMock
            .Verify(
                x => x.UpsertAsync(
                    TestCollectionName,
                    It.Is<IReadOnlyList<PointStruct>>(x =>
                        x.Count == 2 &&
                        (testRecordKeys[0]!.GetType() == typeof(ulong) && x[0].Id.Num == (testRecordKeys[0] as ulong?) || testRecordKeys[0]!.GetType() == typeof(Guid) && x[0].Id.Uuid == (testRecordKeys[0] as Guid?).ToString()) &&
                        (testRecordKeys[1]!.GetType() == typeof(ulong) && x[1].Id.Num == (testRecordKeys[1] as ulong?) || testRecordKeys[1]!.GetType() == typeof(Guid) && x[1].Id.Uuid == (testRecordKeys[1] as Guid?).ToString())),
                    true,
                    null,
                    null,
                    this._testCancellationToken),
                Times.Once);
    }

    [Fact]
    public async Task CanUpsertRecordWithCustomMapperAsync()
    {
        // Arrange.
        this.SetupUpsertMock();
        var pointStruct = new PointStruct
        {
            Id = new() { Num = UlongTestRecordKey1 },
            Payload = { ["Data"] = "data 1" },
            Vectors = new[] { 1f, 2f, 3f, 4f }
        };

        // Arrange mapper mock from data model to PointStruct.
        var mapperMock = new Mock<IVectorStoreRecordMapper<SinglePropsModel<ulong>, PointStruct>>(MockBehavior.Strict);
        mapperMock
            .Setup(x => x.MapFromDataToStorageModel(It.IsAny<SinglePropsModel<ulong>>()))
            .Returns(pointStruct);

        // Arrange target with custom mapper.
        var sut = new QdrantVectorRecordStore<SinglePropsModel<ulong>>(
            this._qdrantClientMock.Object,
            new()
            {
                DefaultCollectionName = TestCollectionName,
                HasNamedVectors = false,
                MapperType = QdrantRecordMapperType.QdrantPointStructCustomMapper,
                PointStructCustomMapper = mapperMock.Object
            });

        var model = CreateModel(UlongTestRecordKey1, true);

        // Act
        await sut.UpsertAsync(
            model,
            null,
            this._testCancellationToken);

        // Assert
        mapperMock
            .Verify(
                x => x.MapFromDataToStorageModel(It.Is<SinglePropsModel<ulong>>(x => x == model)),
                Times.Once);
    }

    private void SetupRetrieveMock(List<RetrievedPoint> retrievedPoints)
    {
        this._qdrantClientMock
            .Setup(x => x.RetrieveAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<PointId>>(),
                It.IsAny<bool>(), // With Payload
                It.IsAny<bool>(), // With Vectors
                It.IsAny<ReadConsistency>(),
                It.IsAny<ShardKeySelector>(),
                this._testCancellationToken))
            .ReturnsAsync(retrievedPoints);
    }

    private void SetupDeleteMocks()
    {
        this._qdrantClientMock
            .Setup(x => x.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<ulong>(),
                It.IsAny<bool>(), // wait
                It.IsAny<WriteOrderingType?>(),
                It.IsAny<ShardKeySelector?>(),
                this._testCancellationToken))
            .ReturnsAsync(new UpdateResult());

        this._qdrantClientMock
            .Setup(x => x.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(), // wait
                It.IsAny<WriteOrderingType?>(),
                It.IsAny<ShardKeySelector?>(),
                this._testCancellationToken))
            .ReturnsAsync(new UpdateResult());

        this._qdrantClientMock
            .Setup(x => x.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<bool>(), // wait
                It.IsAny<WriteOrderingType?>(),
                It.IsAny<ShardKeySelector?>(),
                this._testCancellationToken))
            .ReturnsAsync(new UpdateResult());

        this._qdrantClientMock
            .Setup(x => x.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<bool>(), // wait
                It.IsAny<WriteOrderingType?>(),
                It.IsAny<ShardKeySelector?>(),
                this._testCancellationToken))
            .ReturnsAsync(new UpdateResult());
    }

    private void SetupUpsertMock()
    {
        this._qdrantClientMock
            .Setup(x => x.UpsertAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<PointStruct>>(),
                It.IsAny<bool>(), // wait
                It.IsAny<WriteOrderingType?>(),
                It.IsAny<ShardKeySelector?>(),
                this._testCancellationToken))
            .ReturnsAsync(new UpdateResult());
    }

    private static RetrievedPoint CreateRetrievedPoint<TKey>(bool hasNamedVectors, TKey recordKey)
    {
        RetrievedPoint point;
        if (hasNamedVectors)
        {
            var namedVectors = new NamedVectors();
            namedVectors.Vectors.Add("Vector", new[] { 1f, 2f, 3f, 4f });
            point = new RetrievedPoint()
            {
                Payload = { ["Data"] = "data 1" },
                Vectors = new Vectors { Vectors_ = namedVectors }
            };
        }
        else
        {
            point = new RetrievedPoint()
            {
                Payload = { ["Data"] = "data 1" },
                Vectors = new[] { 1f, 2f, 3f, 4f }
            };
        }

        if (recordKey is ulong ulongKey)
        {
            point.Id = ulongKey;
        }

        if (recordKey is Guid guidKey)
        {
            point.Id = guidKey;
        }

        return point;
    }

    private IVectorRecordStore<T, SinglePropsModel<T>> CreateVectorRecordStore<T>(bool useDefinition, bool passCollectionToMethod, bool hasNamedVectors)
    {
        var store = new QdrantVectorRecordStore<SinglePropsModel<T>>(
            this._qdrantClientMock.Object,
            new()
            {
                DefaultCollectionName = passCollectionToMethod ? null : TestCollectionName,
                VectorStoreRecordDefinition = useDefinition ? this._singlePropsDefinition : null,
                HasNamedVectors = hasNamedVectors
            }) as IVectorRecordStore<T, SinglePropsModel<T>>;
        return store!;
    }

    private static SinglePropsModel<T> CreateModel<T>(T key, bool withVectors)
    {
        return new SinglePropsModel<T>
        {
            Key = key,
            Data = "data 1",
            Vector = withVectors ? new float[] { 1, 2, 3, 4 } : null,
            NotAnnotated = null,
        };
    }

    private readonly VectorStoreRecordDefinition _singlePropsDefinition = new()
    {
        Properties =
        [
            new VectorStoreRecordKeyProperty("Key"),
            new VectorStoreRecordDataProperty("Data"),
            new VectorStoreRecordVectorProperty("Vector")
        ]
    };

    public sealed class SinglePropsModel<T>
    {
        [VectorStoreRecordKey]
        public required T Key { get; set; }

        [VectorStoreRecordData]
        public string Data { get; set; } = string.Empty;

        [VectorStoreRecordVector]
        public ReadOnlyMemory<float>? Vector { get; set; }

        public string? NotAnnotated { get; set; }
    }

    public static IEnumerable<object[]> TestOptions
        => GenerateAllCombinations(new object[][] {
                new object[] { true, false },
                new object[] { true, false },
                new object[] { true, false },
                new object[] { typeof(ulong), typeof(Guid) }
        });

    private static object[][] GenerateAllCombinations(object[][] input)
    {
        var counterArray = Enumerable.Range(0, input.Length).Select(x => 0).ToArray();

        // Add each item from the first option set as a separate row.
        object[][] currentCombinations = input[0].Select(x => new object[1] { x }).ToArray();

        // Loop through each additional option set.
        for (int currentOptionSetIndex = 1; currentOptionSetIndex < input.Length; currentOptionSetIndex++)
        {
            var iterationCombinations = new List<object[]>();
            var currentOptionSet = input[currentOptionSetIndex];

            // Loop through each row we have already.
            foreach (var currentCombination in currentCombinations)
            {
                // Add each of the values from the new options set to the current row to generate a new row.
                for (var currentColumnRow = 0; currentColumnRow < currentOptionSet.Length; currentColumnRow++)
                {
                    iterationCombinations.Add(currentCombination.Append(currentOptionSet[currentColumnRow]).ToArray());
                }
            }

            currentCombinations = iterationCombinations.ToArray();
        }

        return currentCombinations;
    }
}
