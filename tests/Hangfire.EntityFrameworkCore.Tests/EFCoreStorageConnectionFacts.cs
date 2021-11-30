using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests;

public class EFCoreStorageConnectionFacts : EFCoreStorageTest
{
    [Fact]
    public static void Ctor_Throws_WhenStorageParameterIsNull()
    {
        EFCoreStorage storage = null;

        Assert.Throws<ArgumentNullException>(nameof(storage),
            () => new EFCoreStorageConnection(storage));
    }

    [Fact]
    public void Ctor_CreatesInstance()
    {
        var storage = CreateStorageStub();

        var instance = new EFCoreStorageConnection(storage);

        Assert.Same(storage, Assert.IsType<EFCoreStorage>(instance.GetFieldValue("_storage")));
    }

    [Fact]
    public void AcquireLock_Throws_WhenResourceParameterIsNull()
    {
        string resource = null;
        var timeout = new TimeSpan(1);
        var instance = CreateConnection();

        Assert.Throws<ArgumentNullException>(nameof(resource),
            () => instance.AcquireDistributedLock(null, timeout));
    }

    [Fact]
    public void AcquireLock_Throws_WhenResourceParameterIsEmpty()
    {
        string resource = string.Empty;
        var timeout = new TimeSpan(1);
        var instance = CreateConnection();

        Assert.Throws<ArgumentException>(nameof(resource),
            () => instance.AcquireDistributedLock(string.Empty, timeout));
    }

    [Fact]
    public void AcquireLock_Throws_WhenTimeoutParameterIsNegative()
    {
        string resource = "resource";
        var timeout = new TimeSpan(-1);
        var instance = CreateConnection();

        Assert.Throws<ArgumentOutOfRangeException>(nameof(timeout),
            () => instance.AcquireDistributedLock(resource, timeout));
    }

    [Fact]
    public void AcquireLock_ReturnsLockInstance()
    {
        var result = UseConnection(instance => instance.AcquireDistributedLock("1", TimeSpan.FromSeconds(1)));

        Assert.NotNull(result);
        using (result)
            Assert.IsType<EFCoreLock>(result);
    }

    [Fact]
    public void AnnounceServer_Throws_WhenServerIdParameterIsNull()
    {
        string serverId = null;
        var context = new ServerContext();

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(serverId),
            () => instance.AnnounceServer(serverId, context)));
    }

    [Fact]
    public void AnnounceServer_Throws_WhenContextParameterIsNull()
    {
        string serverId = "server";
        ServerContext context = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(context),
            () => instance.AnnounceServer(serverId, context)));
    }

    [Fact]
    public void AnnounceServer_CreatesOrUpdatesARecord()
    {
        var serverId = "server";
        var serverContext1 = new ServerContext
        {
            Queues = new[] { "critical", "default" },
            WorkerCount = 4
        };
        var serverContext2 = new ServerContext
        {
            Queues = new[] { "default" },
            WorkerCount = 1000
        };
        UseConnection(instance =>
        {
            var timestampBeforeBegin = DateTime.UtcNow;

            instance.AnnounceServer(serverId, serverContext1);

            var timestampAfterEnd = DateTime.UtcNow;
            CheckServer(serverId, serverContext1, timestampBeforeBegin, timestampAfterEnd);
            timestampBeforeBegin = DateTime.UtcNow;

            instance.AnnounceServer(serverId, serverContext2);

            timestampAfterEnd = DateTime.UtcNow;
            CheckServer(serverId, serverContext2, timestampBeforeBegin, timestampAfterEnd);
        });
    }

    [Fact]
    public void CreateWriteTransaction_ReturnsCorectResult()
    {
        var instance = CreateConnection();

        var result = instance.CreateWriteTransaction();

        Assert.NotNull(result);
        using (result)
            Assert.IsType<EFCoreStorageTransaction>(result);
    }

    [Fact]
    public void CreateExpiredJob_Throws_WhenJobParameterIsNull()
    {
        Job job = null;
        var parameters = new Dictionary<string, string>();
        var createdAt = DateTime.UtcNow;
        var expireIn = TimeSpan.Zero;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(job),
            () => instance.CreateExpiredJob(job, parameters, createdAt, expireIn)));
    }

    [Fact]
    public void CreateExpiredJob_Throws_WhenParametersParameterIsNull()
    {
        var job = Job.FromExpression(() => SampleMethod("argument"));
        Dictionary<string, string> parameters = null;
        var createdAt = DateTime.UtcNow;
        var expireIn = TimeSpan.Zero;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(parameters),
            () => instance.CreateExpiredJob(job, parameters, createdAt, expireIn)));
    }

    [Fact]
    public void CreateExpiredJob_CreatesAJobInTheStorage_AndSetsItsParameters()
    {
        var createdAt = DateTime.UtcNow;

        var jobId = UseConnection(instance =>
            instance.CreateExpiredJob(
                Job.FromExpression(() => SampleMethod("argument")),
                new Dictionary<string, string>
                {
                    ["Key1"] = "Value1",
                    ["Key2"] = "Value2",
                },
                createdAt,
                TimeSpan.FromDays(1)));

        Assert.NotNull(jobId);
        Assert.NotEmpty(jobId);

        UseContext(context =>
        {
            var hangfireJob = context.Set<HangfireJob>().
                Include(p => p.Parameters).
                Single();

            var invocationData = SerializationHelper.Deserialize<InvocationData>(hangfireJob.InvocationData);

            Assert.Equal(jobId, hangfireJob.Id.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(createdAt, hangfireJob.CreatedAt);
            Assert.Null(hangfireJob.State);
            Assert.Null(hangfireJob.StateName);
            var job = invocationData.DeserializeJob();
            Assert.Equal(typeof(EFCoreStorageTest), job.Type);
            Assert.Equal(nameof(SampleMethod), job.Method.Name);
            Assert.Equal("argument", job.Args[0]);
            Assert.True(createdAt.AddDays(1).AddMinutes(-1) < hangfireJob.ExpireAt);
            Assert.True(hangfireJob.ExpireAt < createdAt.AddDays(1).AddMinutes(1));
            var parameters = hangfireJob.Parameters.ToDictionary(x => x.Name, x => x.Value);
            Assert.Equal("Value1", parameters["Key1"]);
            Assert.Equal("Value2", parameters["Key2"]);
        });
    }

    [Fact]
    public void CreateExpiredJob_CanCreateParametersWithNullValues()
    {
        var createdAt = DateTime.UtcNow;
        var jobId = UseConnection(instance =>
            instance.CreateExpiredJob(
                Job.FromExpression(() => SampleMethod("argument")),
                new Dictionary<string, string>
                {
                    ["Key1"] = null,
                },
                createdAt,
                TimeSpan.FromDays(1)));

        Assert.NotNull(jobId);
        Assert.NotEmpty(jobId);

        UseContext(context =>
        {
            var hangfireJob = context.Set<HangfireJob>().
                Include(p => p.Parameters).
                Single();

            Assert.Equal(jobId, hangfireJob.Id.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(createdAt, hangfireJob.CreatedAt);
            Assert.Null(hangfireJob.State);
            Assert.Null(hangfireJob.StateName);

            var invocationData = SerializationHelper.Deserialize<InvocationData>(hangfireJob.InvocationData);

            var job = invocationData.DeserializeJob();
            Assert.Equal(typeof(EFCoreStorageTest), job.Type);
            Assert.Equal(nameof(SampleMethod), job.Method.Name);
            Assert.Equal("argument", job.Args[0]);
            Assert.True(createdAt.AddDays(1).AddMinutes(-1) < hangfireJob.ExpireAt);
            Assert.True(hangfireJob.ExpireAt < createdAt.AddDays(1).AddMinutes(1));
            var parameters = hangfireJob.Parameters.ToDictionary(x => x.Name, x => x.Value);
            Assert.Null(parameters["Key1"]);
        });
    }

    [Fact]
    public void FetchNextJob_Throws_IfQueuesParameterIsNull()
    {
        string[] queues = null;
        var cancellationToken = CancellationToken.None;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(queues),
            () => instance.FetchNextJob(queues, cancellationToken)));
    }

    [Fact]
    public void FetchNextJob_Throws_IfQueuesParameterIsEmpty()
    {
        string[] queues = Array.Empty<string>();
        var cancellationToken = CancellationToken.None;

        UseConnection(instance => Assert.Throws<ArgumentException>(nameof(queues),
            () => instance.FetchNextJob(queues, cancellationToken)));
    }

    [Fact]
    public void FetchNextJob_ReturnsFetchedJob()
    {
        var queue = "queue";
        var job = new HangfireJob
        {
            InvocationData = InvocationDataStub,
        };
        job.QueuedJobs.Add(new HangfireQueuedJob
        {
            Job = job,
            Queue = queue,
            FetchedAt = null,
        });
        UseContextSavingChanges(context => context.Add(job));
        UseConnection(instance =>
        {
            var cancellationToken = CancellationToken.None;
            var queues = new[] { queue };

            var result = instance.FetchNextJob(queues, cancellationToken);

            Assert.NotNull(result);
            using (result)
            {
                var fetchedJob = Assert.IsType<EFCoreFetchedJob>(result);
            }

        });
    }

    [Fact]
    public void GetAllEntriesFromHash_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetAllEntriesFromHash(key)));
    }

    [Fact]
    public void GetAllEntriesFromHash_ReturnsNull_IfHashDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetAllEntriesFromHash(key));

        Assert.Null(result);
    }

    [Fact]
    public void GetAllEntriesFromHash_ReturnsAllKeysAndTheirValues()
    {
        string key1 = "key1";
        string key2 = "key2";

        var hangfireHashes = new[]
        {
                new HangfireHash
                {
                    Key = key1,
                    Field = "Key1",
                    Value = "Value1",
                },
                new HangfireHash
                {
                    Key = key1,
                    Field = "Key2",
                    Value = "Value2",
                },
                new HangfireHash
                {
                    Key = key2,
                    Field = "Key3",
                    Value = "Value3",
                },
            };
        UseContextSavingChanges(context => context.AddRange(hangfireHashes));

        var result = UseConnection(instance => instance.GetAllEntriesFromHash(key1));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Value1", result["Key1"]);
        Assert.Equal("Value2", result["Key2"]);
    }

    [Fact]
    public void GetAllItemsFromList_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetAllItemsFromList(key)));
    }

    [Fact]
    public void GetAllItemsFromList_ReturnsAnEmptyList_WhenListDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetAllItemsFromList(key));

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllItemsFromList_ReturnsAllItems_FromAGivenList()
    {
        string key1 = "key1";
        string key2 = "key2";
        var lists = new[]
        {
                new HangfireList
                {
                    Key = key1,
                    Position = 0,
                    Value = "1",
                },
                new HangfireList
                {
                    Key = key2,
                    Position = 0,
                    Value = "2",
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 1,
                    Value = "3"
                , },
            };
        UseContextSavingChanges(context => context.AddRange(lists));

        var result = UseConnection(instance => instance.GetAllItemsFromList(key1));

        Assert.Equal(new[] { "3", "1" }, result);
    }

    [Fact]
    public void GetAllItemsFromSet_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetAllItemsFromSet(key)));
    }

    [Fact]
    public void GetAllItemsFromSet_ReturnsEmptyCollection_WhenKeyDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetAllItemsFromSet(key));

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllItemsFromSet_ReturnsAllItems()
    {
        string key = "key";

        var sets = new[]
        {
                new HangfireSet
                {
                    Key = key,
                    Value = "1",
                },
                new HangfireSet
                {
                    Key = key,
                    Value = "2",
                },
            };
        UseContextSavingChanges(context => context.AddRange(sets));

        var result = UseConnection(instance => instance.GetAllItemsFromSet(key));

        Assert.Equal(sets.Length, result.Count);
        Assert.Contains("1", result);
        Assert.Contains("2", result);
    }

    [Fact]
    public void GetCounter_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetCounter(key)));
    }

    [Fact]
    public void GetCounter_ReturnsZero_WhenKeyDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetCounter(key));

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetCounter_ReturnsSumOfValues_InCounterTable()
    {
        string key1 = "key1";
        string key2 = "key2";

        var counters = new[]
        {
                new HangfireCounter
                {
                    Key = key1,
                    Value = 1,
                },
                new HangfireCounter
                {
                    Key = key2,
                    Value = 1,
                },
                new HangfireCounter
                {
                    Key = key1,
                    Value = 1,
                },
            };
        UseContextSavingChanges(context => context.AddRange(counters));

        var result = UseConnection(instance => instance.GetCounter(key1));

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetFirstByLowestScoreFromSet_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetFirstByLowestScoreFromSet(key, 0, 1)));
    }

    [Theory]
    [InlineData(-1.0, 3.0)]
    [InlineData(3.0, -1.0)]
    public void GetFirstByLowestScoreFromSet_ReturnsTheValueWithTheLowestScore(double fromScore, double toScore)
    {
        string key1 = "key1";
        string key2 = "key2";

        var sets = new[]
        {
                new HangfireSet
                {
                    Key = key1,
                    Value = "1.0",
                    Score = 1.0,
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "-1.0",
                    Score = -1.0,
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "-5.0",
                    Score = -5.0,
                },
                new HangfireSet
                {
                    Key = key2,
                    Value = "-2.0",
                    Score = -2.0,
                },
            };
        UseContextSavingChanges(context => context.AddRange(sets));

        var result = UseConnection(instance => instance.GetFirstByLowestScoreFromSet(key1, fromScore, toScore));

        Assert.Equal("-1.0", result);
    }

    [Fact]
    public void GetJobData_Throws_WhenJobIdParameterIsNull()
    {
        string jobId = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(jobId),
            () => instance.GetJobData(jobId)));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GetJobData_ReturnsNull_WhenThereIsNoSuchJob(string jobId)
    {
        var result = UseConnection(instance => instance.GetJobData(jobId));

        Assert.Null(result);
    }

    [Fact]
    public void GetJobData_ReturnsJobLoadException_IfThereWasADeserializationException()
    {
        var createdAt = DateTime.UtcNow;
        var job = new HangfireJob
        {
            CreatedAt = createdAt,
            InvocationData = InvocationDataStub,
        };
        var state = new HangfireState
        {
            Name = "state",
            Reason = "reason",
            Data = EmptyDictionaryStub,
        };
        job.States.Add(state);
        UseContextSavingChanges(context =>
        {
            context.Add(job);
            context.SaveChanges();
            job.State = state;
            job.StateName = state.Name;
        });
        var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        var result = UseConnection(instance => instance.GetJobData(jobId));

        Assert.Null(result.Job);
        Assert.Equal("state", result.State);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.NotNull(result.LoadException);
    }

    [Fact]
    public void GetJobData_ReturnsResult_WhenJobExists()
    {
        var createdAt = DateTime.UtcNow;
        var job = new HangfireJob
        {
            CreatedAt = createdAt,
            InvocationData = CreateInvocationData(() => SampleMethod("Arguments")),
        };
        var state = new HangfireState
        {
            Name = "state",
            Reason = "reason",
            Data = EmptyDictionaryStub,
        };
        job.States.Add(state);
        UseContextSavingChanges(context =>
        {
            context.Add(job);
            context.SaveChanges();
            job.State = state;
            job.StateName = state.Name;
        });
        var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        var result = UseConnection(instance => instance.GetJobData(jobId));

        Assert.NotNull(result);
        Assert.NotNull(result.Job);
        Assert.Equal("state", result.State);
        Assert.Equal("Arguments", result.Job.Args[0]);
        Assert.Null(result.LoadException);
        Assert.Equal(createdAt, result.CreatedAt);
    }

    [Fact]
    public void GetJobParameter_Throws_WhenJobIdParameterIsNull()
    {
        string id = null;
        string name = "name";

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(id),
            () => instance.GetJobParameter(id, name)));
    }

    [Fact]
    public void GetJobParameter_Throws_WhenNameParameterIsNull()
    {
        string id = "1";
        string name = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(name),
            () => instance.GetJobParameter(id, name)));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GetJobParameter_ReturnsNull_WhenParameterDoesNotExists(string id)
    {
        string name = "1";
        var value = UseConnection(instance => instance.GetJobParameter(id, name));

        Assert.Null(value);
    }

    [Fact]
    public void GetJobParameter_ReturnsParameterValue_WhenJobExists()
    {
        var parameterName = "name";
        var parameterValue = "value";

        var job = new HangfireJob
        {
            InvocationData = InvocationDataStub,
            Parameters = new[]
            {
                    new HangfireJobParameter
                    {
                        Name = parameterName,
                        Value = parameterValue,
                    }
                },
        };
        UseContextSavingChanges(context => context.Add(job));
        var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        var value = UseConnection(instance => instance.GetJobParameter(jobId, parameterName));

        Assert.Equal(parameterValue, value);
    }

    [Fact]
    public void GetHashCount_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetHashCount(key)));
    }

    [Fact]
    public void GetHashCount_ReturnsZero_WhenKeyDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetHashCount(key));

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetHashCount_ReturnsNumber_OfHashFields()
    {
        string key1 = "key1";
        string key2 = "key2";

        var hangfireHashes = new[]
        {
                new HangfireHash
                {
                    Key = key1,
                    Field = "field-1",
                },
                new HangfireHash
                {
                    Key = key1,
                    Field = "field-2",
                },
                new HangfireHash
                {
                    Key = key2,
                    Field = "field-1",
                },
            };
        UseContextSavingChanges(context => context.AddRange(hangfireHashes));

        var result = UseConnection(instance => instance.GetHashCount(key1));

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetHashTtl_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetHashTtl(key)));
    }

    [Fact]
    public void GetHashTtl_ReturnsNegativeValue_WhenHashDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetHashTtl(key));

        Assert.Equal(new TimeSpan(0, 0, -1), result);
    }

    [Fact]
    public void GetHashTtl_ReturnsExpirationTimeForHash()
    {
        string key1 = "key1";
        string key2 = "key2";

        var hangfireHashes = new[]
        {
                new HangfireHash
                {
                    Key = key1,
                    Field = "field",
                    ExpireAt = DateTime.UtcNow.AddHours(1),
                },
                new HangfireHash
                {
                    Key = key2,
                    Field = "field",
                },
            };
        UseContextSavingChanges(context => context.AddRange(hangfireHashes));

        var result = UseConnection(instance => instance.GetHashTtl(key1));

        Assert.True(TimeSpan.FromMinutes(59) < result);
        Assert.True(result < TimeSpan.FromMinutes(61));
    }

    [Fact]
    public void GetListCount_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetListCount(key)));
    }

    [Fact]
    public void GetListCount_ReturnsZero_WhenListDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetListCount(key));

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetListCount_ReturnsTheNumberOfListElements()
    {
        string key1 = "key1";
        string key2 = "key2";

        var lists = new[]
        {
                new HangfireList
                {
                    Key = key1,
                    Position = 0,
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 1,
                },
                new HangfireList
                {
                    Key = key2,
                    Position = 0,
                },
            };
        UseContextSavingChanges(context => context.AddRange(lists));

        var result = UseConnection(instance => instance.GetListCount(key1));

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetListTtl_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetListTtl(key)));
    }

    [Fact]
    public void GetListTtl_ReturnsNegativeValue_WhenListDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetListTtl(key));

        Assert.Equal(new TimeSpan(0, 0, -1), result);
    }

    [Fact]
    public void GetListTtl_ReturnsExpirationTimeForList()
    {
        string key1 = "key1";
        string key2 = "key2";

        var lists = new[]
        {
                new HangfireList
                {
                    Key = key1,
                    Position = 0,
                    ExpireAt = DateTime.UtcNow.AddHours(1)
                },
                new HangfireList
                {
                    Key = key2,
                    Position = 0,
                },
            };
        UseContextSavingChanges(context => context.AddRange(lists));

        var result = UseConnection(instance => instance.GetListTtl(key1));

        Assert.True(TimeSpan.FromMinutes(59) < result);
        Assert.True(result < TimeSpan.FromMinutes(61));
    }

    [Fact]
    public void GetRangeFromList_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetRangeFromList(key, 0, 1)));
    }

    [Fact]
    public void GetRangeFromList_ReturnsAnEmptyList_WhenListDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetRangeFromList(key, 0, 1));

        Assert.Empty(result);
    }

    [Fact]
    public void GetRangeFromList_ReturnsAllEntries_WithinGivenBounds()
    {
        string key1 = "key1";
        string key2 = "key2";

        var lists = new[]
        {
                new HangfireList
                {
                    Key = key1,
                    Position = 0,
                    Value = "1",
                },
                new HangfireList
                {
                    Key = key2,
                    Position = 0,
                    Value = "2",
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 1,
                    Value = "3",
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 2,
                    Value = "4",
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 3,
                    Value = "5",
                },
            };
        UseContextSavingChanges(context => context.AddRange(lists));

        var result = UseConnection(instance => instance.GetRangeFromList(key1, 1, 2));

        Assert.Equal(new[] { "4", "3" }, result);
    }

    [Fact]
    public void GetRangeFromList_ReturnsAllEntries_WithinGivenInvertedBounds()
    {
        string key1 = "key1";
        string key2 = "key2";

        var lists = new[]
        {
                new HangfireList
                {
                    Key = key1,
                    Position = 0,
                    Value = "1",
                },
                new HangfireList
                {
                    Key = key2,
                    Position = 0,
                    Value = "2",
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 1,
                    Value = "3",
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 2,
                    Value = "4",
                },
                new HangfireList
                {
                    Key = key1,
                    Position = 3,
                    Value = "5",
                },
            };
        UseContextSavingChanges(context => context.AddRange(lists));

        var result = UseConnection(instance => instance.GetRangeFromList(key1, 2, 1));

        Assert.Equal(new[] { "4", "3" }, result);
    }

    [Fact]
    public void GetRangeFromSet_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetRangeFromSet(key, 0, 1)));
    }

    [Fact]
    public void GetRangeFromSet_ReturnsPagedElements()
    {
        string key1 = "key1";
        string key2 = "key2";

        var sets = new[]
        {
                new HangfireSet
                {
                    Key = key1,
                    Value = "1",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "2",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "3",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "4",
                },
                new HangfireSet
                {
                    Key = key2,
                    Value = "4",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "5",
                },
            };
        UseContextSavingChanges(context => context.AddRange(sets));

        var result = UseConnection(instance => instance.GetRangeFromSet(key1, 2, 3));

        Assert.Equal(new[] { "3", "4" }, result);
    }

    [Fact]
    public void GetRangeFromSet_ReturnsPagedElements_WhenBoundariesInverted()
    {
        string key1 = "key1";
        string key2 = "key2";

        var sets = new[]
        {
                new HangfireSet
                {
                    Key = key1,
                    Value = "1",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "2",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "3",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "4",
                },
                new HangfireSet
                {
                    Key = key2,
                    Value = "4",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "5",
                },
            };
        UseContextSavingChanges(context => context.AddRange(sets));

        var result = UseConnection(instance => instance.GetRangeFromSet(key1, 3, 2));

        Assert.Equal(new[] { "3", "4" }, result);
    }

    [Fact]
    public void GetSetCount_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetSetCount(key)));
    }

    [Fact]
    public void GetSetCount_ReturnsZero_WhenSetDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetSetCount(key));

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetSetCount_ReturnsNumberOfElements_InASet()
    {
        string key1 = "key1";
        string key2 = "key2";

        var sets = new[]
        {
                new HangfireSet
                {
                    Key = key1,
                    Value = "1",
                },
                new HangfireSet
                {
                    Key = key2,
                    Value = "1",
                },
                new HangfireSet
                {
                    Key = key1,
                    Value = "2",
                },
            };
        UseContextSavingChanges(context => context.AddRange(sets));

        var result = UseConnection(instance => instance.GetSetCount(key1));

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetSetTtl_Throws_WhenKeyParameterIsNull()
    {
        string key = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetSetTtl(key)));
    }

    [Fact]
    public void GetSetTtl_ReturnsNegativeValue_WhenSetDoesNotExist()
    {
        string key = "key";

        var result = UseConnection(instance => instance.GetSetTtl(key));

        Assert.True(result < TimeSpan.Zero);
    }

    [Fact]
    public void GetSetTtl_ReturnsExpirationTime_OfAGivenSet()
    {
        string key1 = "key1";
        string key2 = "key2";

        var sets = new[]
        {
                new HangfireSet
                {
                    Key = key1,
                    Value = "1",
                    ExpireAt = DateTime.UtcNow.AddHours(1),
                },
                new HangfireSet
                {
                    Key = key2,
                    Value = "2",
                },
            };
        UseContextSavingChanges(context => context.AddRange(sets));

        var result = UseConnection(instance => instance.GetSetTtl(key1));

        Assert.True(TimeSpan.FromMinutes(59) < result);
        Assert.True(result < TimeSpan.FromMinutes(61));
    }

    [Fact]
    public void GetStateData_Throws_WhenJobIdParameterIsNull()
    {
        string jobId = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(jobId),
            () => instance.GetStateData(jobId)));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GetStateData_ReturnsNull_IfThereIsNoSuchState(string jobId)
    {
        var result = UseConnection(instance => instance.GetStateData(jobId));

        Assert.Null(result);
    }

    [Fact]
    public void GetStateData_ReturnsCorrectData()
    {
        var stateName = "StateName1";
        var job = new HangfireJob
        {
            InvocationData = CreateInvocationData(() => SampleMethod("Arguments")),
        };
        var data = new Dictionary<string, string>
        {
            ["Key"] = "Value",
        };
        var state = new HangfireState
        {
            Name = stateName,
            Reason = "Reason",
            Data = SerializationHelper.Serialize(data),
        };
        job.States.Add(state);
        UseContextSavingChanges(context =>
        {
            context.Add(job);
            context.SaveChanges();
            job.State = state;
            job.StateName = state.Name;
        });
        string jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        var result = UseConnection(instance => instance.GetStateData(jobId));

        Assert.NotNull(result);

        Assert.Equal(stateName, result.Name);
        Assert.Equal("Reason", result.Reason);
        Assert.Equal("Value", result.Data["Key"]);
    }

    [Fact]
    public void GetStateData_ReturnsCorrectData_WhenPropertiesAreCamelcased()
    {
        var stateName = "StateName1";
        var job = new HangfireJob
        {
            InvocationData = CreateInvocationData(() => SampleMethod("Arguments")),
        };
        var data = new Dictionary<string, string>
        {
            ["key"] = "Value",
        };
        var state = new HangfireState
        {
            CreatedAt = DateTime.UtcNow,
            Name = stateName,
            Reason = "Reason",
            Data = SerializationHelper.Serialize(data),
        };
        job.States.Add(state);
        UseContextSavingChanges(context =>
        {
            context.Add(job);
            context.SaveChanges();
            job.State = state;
            job.StateName = state.Name;
        });

        string jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        var result = UseConnection(instance => instance.GetStateData(jobId));

        Assert.NotNull(result);

        Assert.Equal(stateName, result.Name);
        Assert.Equal("Reason", result.Reason);
        Assert.Equal("Value", result.Data["key"]);
    }

    [Fact]
    public void GetValueFromHash_Throws_WhenKeyParameterIsNull()
    {
        string key = null;
        string name = "name";

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.GetValueFromHash(key, name)));

    }

    [Fact]
    public void GetValueFromHash_Throws_WhenNameParameterIsNull()
    {
        string key = "key";
        string name = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(name),
            () => instance.GetValueFromHash(key, name)));
    }

    [Fact]
    public void GetValueFromHash_ReturnsNull_WhenHashDoesNotExist()
    {
        string key = "key";
        string name = "name";

        var result = UseConnection(instance => instance.GetValueFromHash(key, name));

        Assert.Null(result);
    }

    [Fact]
    public void GetValueFromHash_ReturnsValue_OfAGivenField()
    {
        string key1 = "key1";
        string key2 = "key2";

        var hangfireHashes = new[]
        {
                new HangfireHash
                {
                    Key = key1,
                    Field = "field-1",
                    Value = "1",
                },
                new HangfireHash
                {
                    Key = key1,
                    Field = "field-2",
                    Value = "2",
                },
                new HangfireHash
                {
                    Key = key2,
                    Field = "field-1",
                    Value = "3",
                },
            };
        UseContextSavingChanges(context => context.AddRange(hangfireHashes));

        var result = UseConnection(instance => instance.GetValueFromHash(key1, "field-1"));

        Assert.Equal("1", result);
    }

    [Fact]
    public void Heartbeat_Throws_WhenServerIdParameterIsNull()
    {
        string serverId = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(serverId),
            () => instance.Heartbeat(serverId)));
    }

    [Fact]
    public void Heartbeat_DoesNotThrow_WhenServerIsNotExisting()
    {
        string serverId = "server1";

        UseConnection(instance => instance.Heartbeat(serverId));
    }

    [Fact]
    public void Heartbeat_UpdatesLastHeartbeat_OfTheServerWithGivenId()
    {
        string server1 = "server1";
        string server2 = "server2";

        var datetime = new DateTime(2017, 1, 1, 11, 22, 33);

        var servers = new[]
        {
                new HangfireServer
                {
                    Id = server1,
                    StartedAt = datetime,
                    Heartbeat = datetime,
                    Queues = EmptyArrayStub,
                },
                new HangfireServer
                {
                    Id = server2,
                    StartedAt = datetime,
                    Heartbeat = datetime,
                    Queues = EmptyArrayStub,
                },
            };
        UseContextSavingChanges(context => context.AddRange(servers));

        UseConnection(instance => instance.Heartbeat("server1"));

        UseContext(context =>
        {
            DateTime GetHeartbeatByServerId(string serverId) => (
                from server in context.Set<HangfireServer>()
                where server.Id == serverId
                select server.Heartbeat).
                Single();

            DateTime
                actualHeartbeat1 = GetHeartbeatByServerId(server1),
                actualHeartbeat2 = GetHeartbeatByServerId(server2);

            Assert.NotEqual(datetime, actualHeartbeat1);
            Assert.Equal(datetime, actualHeartbeat2);
        });
    }

    [Fact]
    public void RemoveServer_Throws_WhenServerIdParameterIsNull()
    {
        string serverId = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(serverId),
            () => instance.RemoveServer(serverId)));
    }

    [Fact]
    public void RemoveServer_RemovesAServerRecord()
    {
        var serverId = "Server1";
        var startedAt = new DateTime(2017, 1, 1, 11, 33, 33);
        var server = new HangfireServer
        {
            Id = serverId,
            StartedAt = startedAt,
            Heartbeat = DateTime.UtcNow,
            Queues = EmptyArrayStub,
        };
        UseContextSavingChanges(context => context.Add(server));

        UseConnection(instance => instance.RemoveServer(serverId));

        UseContext(context => Assert.False(context.Set<HangfireServer>().
            Any(x => x.Id == serverId)));
    }

    [Fact]
    public void RemoveTimedOutServers_DoItsWorkPerfectly()
    {
        string server1 = "server1";
        string server2 = "server2";
        var startedAt = new DateTime(2017, 1, 1, 11, 33, 33);
        var servers = new[]
        {
                new HangfireServer
                {
                    Id = server1,
                    StartedAt = startedAt,
                    Heartbeat = DateTime.UtcNow.AddHours(-1),
                    Queues = EmptyArrayStub,
                },
                new HangfireServer
                {
                    Id = server2,
                    StartedAt = startedAt,
                    Heartbeat = DateTime.UtcNow.AddHours(-3),
                    Queues = EmptyArrayStub,
                },
            };
        UseContextSavingChanges(context => context.AddRange(servers));

        UseConnection(instance => instance.RemoveTimedOutServers(TimeSpan.FromHours(2)));

        UseContext(context =>
        {
            var dbSet = context.Set<HangfireServer>();
            Assert.Single(dbSet.Where(x => x.Id == server1));
            Assert.Empty(dbSet.Where(x => x.Id == server2));
        });
    }

    [Fact]
    public void SetJobParameter_Throws_WhenJobIdParameterIsNull()
    {
        string id = null;
        string name = "name";
        string value = "value";
        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(id),
            () => instance.SetJobParameter(id, name, value)));
    }

    [Fact]
    public void SetJobParameter_Throws_WhenJobIdParameterIsEmpty()
    {
        string id = string.Empty;
        string name = "name";
        string value = "value";

        UseConnection(instance => Assert.Throws<ArgumentException>(nameof(id),
            () => instance.SetJobParameter(id, name, value)));
    }

    [Fact]
    public void SetJobParameter_Throws_WhenNameParameterIsNull()
    {
        string id = "1";
        string name = null;
        string value = "value";
        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(name),
            () => instance.SetJobParameter(id, name, value)));
    }

    [Fact]
    public void RemoveTimedOutServers_Throws_WhenTimeOutIsNegative()
    {
        var timeOut = new TimeSpan(-1);
        UseConnection(instance => Assert.Throws<ArgumentOutOfRangeException>(nameof(timeOut),
            () => instance.RemoveTimedOutServers(timeOut)));
    }

    [Fact]
    public void SetJobParameter_CreatesNewParameter_WhenParameterWithTheGivenNameDoesNotExists()
    {
        var parameterName = "name";
        var parameterValue = "value";

        var job = new HangfireJob
        {
            InvocationData = InvocationDataStub,
        };
        UseContextSavingChanges(context => context.Add(job));
        var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        UseConnection(instance => instance.SetJobParameter(
            jobId,
            parameterName,
            parameterValue));

        UseContext(context =>
        {
            var result = (
                from parameter in context.Set<HangfireJobParameter>()
                where parameter.JobId == job.Id && parameter.Name == parameterName
                select parameter.Value).
                Single();
            Assert.Equal(parameterValue, result);
        });
    }

    [Fact]
    public void SetJobParameter_UpdatesValue_WhenParameterWithTheGivenName_AlreadyExists()
    {
        var parameterName = "name";
        var parameterValue = "value";
        var parameterAnotherValue = "another-value";
        var job = new HangfireJob
        {
            InvocationData = InvocationDataStub,
        };
        UseContextSavingChanges(context =>
        {
            context.Add(job);
            context.Add(new HangfireJobParameter
            {
                Job = job,
                Name = parameterName,
                Value = parameterValue,
            });
        });
        var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        UseConnection(instance => instance.SetJobParameter(jobId, parameterName, parameterAnotherValue));

        UseContext(context =>
        {
            var result = (
                from parameter in context.Set<HangfireJobParameter>()
                where parameter.JobId == job.Id && parameter.Name == parameterName
                select parameter.Value).
                Single();
            Assert.Equal(parameterAnotherValue, result);
        });
    }

    [Fact]
    public void SetJobParameter_CanAcceptNulls_AsValues()
    {
        var parameterName = "name";
        var job = new HangfireJob
        {
            InvocationData = InvocationDataStub,
        };
        UseContextSavingChanges(context => context.Add(job));
        var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        UseConnection(instance => instance.SetJobParameter(jobId, parameterName, null));

        UseContext(context =>
        {
            var result = (
                from parameter in context.Set<HangfireJobParameter>()
                where parameter.JobId == job.Id && parameter.Name == parameterName
                select parameter.Value).
                Single();
            Assert.Null(result);
        });
    }

    [Fact]
    public void SetRangeInHash_Throws_WhenKeyParameterIsNull()
    {
        string key = null;
        var keyValuePairs = new Dictionary<string, string>();

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(key),
            () => instance.SetRangeInHash(key, keyValuePairs)));
    }

    [Fact]
    public void SetRangeInHash_Throws_WhenKeyValuePairsParameterIsNull()
    {
        string key = "key";
        Dictionary<string, string> keyValuePairs = null;

        UseConnection(instance => Assert.Throws<ArgumentNullException>(nameof(keyValuePairs),
            () => instance.SetRangeInHash(key, keyValuePairs)));
    }

    [Fact]
    public void SetRangeInHash_AddsAllRecords()
    {
        string key = "key";
        var keyValuePairs = new Dictionary<string, string>
        {
            ["Field1"] = "Value1",
            ["Field2"] = "Value2",
        };

        UseConnection(instance => instance.SetRangeInHash(key, keyValuePairs));

        UseContext(context =>
        {
            var result = (
                from hash in context.Set<HangfireHash>()
                where hash.Key == key
                select new { hash.Field, hash.Value }).
                ToDictionary(x => x.Field, x => x.Value);
            Assert.Equal("Value1", result["Field1"]);
            Assert.Equal("Value2", result["Field2"]);
        });
    }

    [Fact]
    public void SetRangeInHash_MergesAllRecords()
    {
        string key = "key";
        var existingHash = new HangfireHash { Key = key, Field = "Field1", Value = "OldValue1" };
        UseContextSavingChanges(context => context.Add(existingHash));
        var keyValuePairs = new Dictionary<string, string>
        {
            ["Field1"] = "Value1",
            ["Field2"] = "Value2",
        };

        UseConnection(instance => instance.SetRangeInHash(key, keyValuePairs));

        UseContext(context =>
        {
            var result = (
                from hash in context.Set<HangfireHash>()
                where hash.Key == key
                select new { hash.Field, hash.Value }).
                ToDictionary(x => x.Field, x => x.Value);
            Assert.Equal("Value1", result["Field1"]);
            Assert.Equal("Value2", result["Field2"]);
        });
    }

    private void UseConnection(Action<EFCoreStorageConnection> action)
    {
        using var instance = CreateConnection();
        action(instance);
    }

    private T UseConnection<T>(Func<EFCoreStorageConnection, T> func)
    {
        T result = default;
        UseConnection(instance =>
        {
            result = func(instance);
        });
        return result;
    }

    private EFCoreStorageConnection CreateConnection()
    {
        return new EFCoreStorageConnection(Storage);
    }

    private void CheckServer(string serverId, ServerContext expectedContext, DateTime timestampBeforeBegin, DateTime timestampAfterEnd)
    {
        UseContext(context =>
        {
            var actualServer = Assert.Single(context.Set<HangfireServer>().
                Where(x => x.Id == serverId));
            var actualQueues = SerializationHelper.Deserialize<string[]>(actualServer.Queues);
            Assert.Equal(serverId, actualServer.Id);
            Assert.Equal(expectedContext.WorkerCount, actualServer.WorkerCount);
            Assert.Equal(expectedContext.Queues, actualQueues);
            Assert.True(timestampBeforeBegin <= actualServer.StartedAt);
            Assert.True(actualServer.StartedAt <= timestampAfterEnd);
        });
    }
}
