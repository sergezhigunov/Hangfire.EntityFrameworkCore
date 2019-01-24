using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EntityFrameworkCoreJobStorageTransactionFacts : HangfireContextTest
    {
        [Fact]
        public void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;
            var queueProvider = new Mock<IPersistentJobQueueProvider>().Object;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new EntityFrameworkCoreJobStorageTransaction(options, queueProvider));
        }

        [Fact]
        public void Ctor_Throws_WhenQueueProviderParameterIsNull()
        {
            var options = new DbContextOptions<HangfireContext>();
            IPersistentJobQueueProvider queueProvider = null;

            Assert.Throws<ArgumentNullException>(nameof(queueProvider),
                () => new EntityFrameworkCoreJobStorageTransaction(options, queueProvider));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var options = new DbContextOptions<HangfireContext>();
            var queueProvider = new Mock<IPersistentJobQueueProvider>().Object;

            var instance = new EntityFrameworkCoreJobStorageTransaction(options, queueProvider);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    instance.GetFieldValue("_options")));
            Assert.Same(queueProvider,
                Assert.IsAssignableFrom<IPersistentJobQueueProvider>(
                    instance.GetFieldValue("_queueProvider")));
            Assert.NotNull(
                Assert.IsType<Queue<Action<HangfireContext>>>(
                    instance.GetFieldValue("_queue")));
            Assert.NotNull(
                Assert.IsType<Queue<Action>>(
                    instance.GetFieldValue("_afterCommitQueue")));
            Assert.False(Assert.IsType<bool>(instance.GetFieldValue("_disposed")));
        }

        [Fact]
        public void Dispose_CleansQueues()
        {
            var options = new DbContextOptions<HangfireContext>();
            var instance = CreateTransaction();
            var queue = Assert.IsType<Queue<Action<HangfireContext>>>(
                instance.GetFieldValue("_queue"));
            var afterCommitQueue = Assert.IsType<Queue<Action>>(
                instance.GetFieldValue("_afterCommitQueue"));
            queue.Enqueue(context => { });
            afterCommitQueue.Enqueue(() => { });

            instance.Dispose();
            Assert.Empty(Assert.IsType<Queue<Action<HangfireContext>>>(
                instance.GetFieldValue("_queue")));
            Assert.Empty(Assert.IsType<Queue<Action>>(
                instance.GetFieldValue("_afterCommitQueue")));

            Assert.True(Assert.IsType<bool>(instance.GetFieldValue("_disposed")));
        }

        [Fact]
        public void AddJobState_Throws_WhenJobIdParameterIsNull()
        {
            string jobId = null;
            var state = new Mock<IState>().Object;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(jobId),
                () => instance.AddJobState(jobId, state));
        }

        [Fact]
        public void AddJobState_Throws_WhenJobIdParameterIsEmpty()
        {
            string jobId = string.Empty;
            var state = new Mock<IState>().Object;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentException>(nameof(jobId),
                () => instance.AddJobState(jobId, state));
        }

        [Fact]
        public void AddJobState_Throws_WhenStateParameterIsNull()
        {
            string jobId = "1";
            IState state = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(state),
                () => instance.AddJobState(jobId, state));
        }

        [Fact]
        public void AddJobState_Throws_WhenTransactionDisposed()
        {
            string jobId = "1";
            var state = new Mock<IState>().Object;

            AssertThrowsObjectDisposed(instance => instance.AddJobState(jobId, state));
        }

        [Fact]
        public void AddJobState_AddsNewRecordToATable()
        {
            var job = InsertJob();
            var jobId = job.Id.ToString(CultureInfo.InvariantCulture);
            var stateMock = new Mock<IState>();
            stateMock.Setup(x => x.Name).Returns("State");
            stateMock.Setup(x => x.Reason).Returns("Reason");
            stateMock.Setup(x => x.SerializeData()).
                Returns(new Dictionary<string, string>
                {
                    ["Name"] = "Value",
                });
            var state = stateMock.Object;
            var createdAtFrom = DateTime.UtcNow;

            UseTransaction(true, instance => instance.AddJobState(jobId, state));

            var createdAtTo = DateTime.UtcNow;
            UseContext(context =>
            {
                var actualJob = Assert.Single(context.Jobs);
                Assert.Null(actualJob.ActualState);
                var jobState = Assert.Single(context.States);
                Assert.Equal("State", jobState.Name);
                Assert.Equal("Reason", jobState.Reason);
                Assert.True(createdAtFrom <= jobState.CreatedAt);
                Assert.True(jobState.CreatedAt <= createdAtTo);
                var data = jobState.Data;
                Assert.Single(data);
                Assert.Equal("Value", data["Name"]);
            });
        }

        [Fact]
        public void AddRangeToSet_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var items = Array.Empty<string>();
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.AddRangeToSet(key, items));
        }

        [Fact]
        public void AddRangeToSet_Throws_WhenItemsParameterIsNull()
        {
            string key = "key";
            IList<string> items = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(items),
                () => instance.AddRangeToSet(key, items));
        }

        [Fact]
        public void AddRangeToSet_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            var items = Array.Empty<string>();

            AssertThrowsObjectDisposed(instance => instance.AddRangeToSet(key, items));
        }

        [Fact]
        public void AddRangeToSet_AddsAllItems()
        {
            string key = "key";
            var items = new List<string>
            {
                "1",
                "2",
                "3",
            };

            UseTransaction(true, instance => instance.AddRangeToSet(key, items));

            UseContext(context =>
            {
                var records = (
                    from set in context.Sets
                    where set.Key == key
                    select set.Value).
                    ToArray();
                Assert.Equal(items, records);
            });

        }

        [Fact]
        public void AddRangeToSet_AddsAllItems_WhenSomeValuesAlreadySet()
        {
            string key = "key";
            var items = new List<string>
            {
                "1",
                "2",
                "3",
            };
            UseContextSavingChanges(context => context.Add(new HangfireSet
            {
                Key = key,
                Value = "1",
            }));

            UseTransaction(true, instance => instance.AddRangeToSet(key, items));

            UseContext(context =>
            {
                var records = (
                    from set in context.Sets
                    where set.Key == key
                    select set.Value).
                    ToArray();
                Assert.Equal(items, records);
            });

        }

        [Fact]
        public void AddToQueue_Throws_WhenQueueParameterIsNull()
        {
            string queue = null;
            string jobId = "1";
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(queue),
                () => instance.AddToQueue(queue, jobId));
        }

        [Fact]
        public void AddToQueue_Throws_WhenQueueParameterIsEmpty()
        {
            string queue = string.Empty;
            string jobId = "1";
            var instance = CreateTransaction();

            Assert.Throws<ArgumentException>(nameof(queue),
                () => instance.AddToQueue(queue, jobId));
        }

        [Fact]
        public void AddToQueue_Throws_WhenJobIdParameterIsNull()
        {
            string queue = "queue";
            string jobId = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(jobId),
                () => instance.AddToQueue(queue, jobId));
        }

        [Fact]
        public void AddToQueue_Throws_WhenJobIdParameterIsEmpty()
        {
            string queue = "queue";
            string jobId = string.Empty;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentException>(nameof(jobId),
                () => instance.AddToQueue(queue, jobId));
        }

        [Fact]
        public void AddToQueue_Throws_WhenTransactionDisposed()
        {
            string queue = "queue";
            string jobId = "1";

            AssertThrowsObjectDisposed(instance => instance.AddToQueue(queue, jobId));
        }

        [Fact]
        public void AddToQueue_CallsEnqueue_OnTargetPersistentQueue()
        {
            var job = InsertJob();
            var jobId = job.Id.ToString(CultureInfo.InvariantCulture);
            var queueMock = new Mock<IPersistentJobQueue>();
            var queueProviderMock = new Mock<IPersistentJobQueueProvider>();
            queueProviderMock.Setup(x => x.GetJobQueue()).Returns(queueMock.Object);
            var queueProvider = queueProviderMock.Object;
            using (var instance = new EntityFrameworkCoreJobStorageTransaction(Options, queueProvider))
            {
                instance.AddToQueue("queue", jobId);
                var queue = Assert.IsType<Queue<Action<HangfireContext>>>(
                    instance.GetFieldValue("_queue"));
                var afterCommitQueue = Assert.IsType<Queue<Action>>(
                    instance.GetFieldValue("_afterCommitQueue"));
                Assert.Single(queue);
                instance.Commit();
            }

            queueMock.Verify(x => x.Enqueue("queue", jobId));
        }

        [Fact]
        public void AddToSet_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            string value = "value";
            double score = 0;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.AddToSet(key, value));
            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.AddToSet(key, value, score));
        }

        [Fact]
        public void AddToSet_Throws_WhenValueParameterIsNull()
        {
            string key = "key";
            string value = null;
            double score = 0;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(value),
                () => instance.AddToSet(key, value));
            Assert.Throws<ArgumentNullException>(nameof(value),
                () => instance.AddToSet(key, value, score));
        }

        [Fact]
        public void AddToSet_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            string value = "value";
            double score = 0;

            AssertThrowsObjectDisposed(instance => instance.AddToSet(key, value));
            AssertThrowsObjectDisposed(instance => instance.AddToSet(key, value, score));
        }

        [Fact]
        public void AddToSet_AddsARecord_IfThereIsNoSuchKeyAndValue()
        {
            string key = "key";

            UseTransaction(true, instance => instance.AddToSet(key, "my-value"));

            UseContext(context =>
            {
                var record = context.Sets.Single();
                Assert.Equal(key, record.Key);
                Assert.Equal("my-value", record.Value);
                Assert.Equal(0m, record.Score);
            });
        }

        [Fact]
        public void AddToSet_AddsARecord_WhenKeyIsExists_ButValuesAreDifferent()
        {
            string key = "key";
            string value = "my-value";
            UseContextSavingChanges(context => context.Add(new HangfireSet
            {
                Key = key,
                Value = value,
            }));

            UseTransaction(true, instance => instance.AddToSet(key, "another-value"));

            UseContext(context => Assert.Equal(2, context.Sets.Count()));
        }

        [Fact]
        public void AddToSet_DoesNotAddARecord_WhenBothKeyAndValueAreExist()
        {
            string key = "key";
            string value = "my-value";
            UseContextSavingChanges(context => context.Add(new HangfireSet
            {
                Key = key,
                Value = value,
            }));

            UseTransaction(true, instance => instance.AddToSet(key, value));

            UseContext(context => Assert.Single(context.Sets));
        }

        [Fact]
        public void AddToSet_WithScore_AddsARecordWithScore_WhenBothKeyAndValueAreNotExist()
        {
            string key = "key";
            string value = "my-value";

            UseTransaction(true, instance => instance.AddToSet(key, value, 3.2));

            UseContext(context =>
            {
                var record = context.Sets.Single();
                Assert.Equal(key, record.Key);
                Assert.Equal(value, record.Value);
                Assert.Equal(3.2m, record.Score);
            });
        }

        [Fact]
        public void AddToSet_WithScore_UpdatesAScore_WhenBothKeyAndValueAreExist()
        {
            string key = "key";
            string value = "my-value";
            UseContextSavingChanges(context => context.Add(new HangfireSet
            {
                Key = key,
                Value = value,
            }));

            UseTransaction(true, instance => instance.AddToSet(key, value, 3.2));

            UseContext(context =>
            {
                var record = context.Sets.Single();
                Assert.Equal(3.2m, record.Score);
            });
        }

        [Fact]
        public void Commit_Throws_WhenTransactionDisposed()
        {
            AssertThrowsObjectDisposed(instance => instance.Commit());
        }

        [Fact]
        public void Commit_DoesNotThrows_WhenContextIsNull()
        {
            UseTransaction(false,
                instance => instance.Commit());
        }

        [Fact]
        public void Commit_DoesNotThrows_WhenContextIsNotNull()
        {
            UseTransaction(false,
                instance =>
                {
                    var queue = Assert.IsType<Queue<Action<HangfireContext>>>(
                        instance.GetFieldValue("_queue"));
                    var afterCommitQueue = Assert.IsType<Queue<Action>>(
                        instance.GetFieldValue("_afterCommitQueue"));

                    bool queueExposed = false;
                    bool afterCommitQueueExposed = false;

                    queue.Enqueue(context =>
                    {
                        Assert.NotNull(context);
                        Assert.False(queueExposed);
                        Assert.False(afterCommitQueueExposed);
                        queueExposed = true;
                    });
                    afterCommitQueue.Enqueue(() =>
                    {
                        Assert.True(queueExposed);
                        Assert.False(afterCommitQueueExposed);
                        afterCommitQueueExposed = true;
                    });
                    Assert.False(queueExposed);
                    Assert.False(afterCommitQueueExposed);

                    instance.Commit();

                    Assert.True(queueExposed);
                    Assert.True(afterCommitQueueExposed);
                });
        }

        [Fact]
        public void DecrementCounter_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var expireIn = new TimeSpan(1, 0, 0);
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.DecrementCounter(key));
            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.DecrementCounter(key, expireIn));
        }

        [Fact]
        public void DecrementCounter_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);

            AssertThrowsObjectDisposed(instance => instance.DecrementCounter(key));
            AssertThrowsObjectDisposed(instance => instance.DecrementCounter(key, expireIn));
        }

        [Fact]
        public void DecrementCounter_AddsRecordToCounterTable_WithoutExpiration()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);

            UseTransaction(true, instance => instance.DecrementCounter(key));

            UseContext(context =>
            {
                var record = context.Counters.Single();
                Assert.Equal(key, record.Key);
                Assert.Equal(-1L, record.Value);
                Assert.Null(record.ExpireAt);
            });
        }

        [Fact]
        public void DecrementCounter_AddsRecordToCounterTable_WithExpiration()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);
            var expiredFrom = DateTime.UtcNow + expireIn;

            UseTransaction(true, instance => instance.DecrementCounter(key, expireIn));

            var expiredTo = DateTime.UtcNow + expireIn;
            UseContext(context =>
            {
                var record = context.Counters.Single();
                Assert.Equal(key, record.Key);
                Assert.Equal(-1L, record.Value);
                Assert.NotNull(record.ExpireAt);
                Assert.True(expiredFrom <= record.ExpireAt);
                Assert.True(expiredTo >= record.ExpireAt);
            });
        }

        [Fact]
        public void ExpireJob_Throws_WhenJobIdParameterIsNull()
        {
            string jobId = null;
            var expireIn = new TimeSpan(1, 0, 0);
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(jobId),
                () => instance.ExpireJob(jobId, expireIn));
        }

        [Fact]
        public void ExpireJob_Throws_WhenJobIdParameterIsEmpty()
        {
            string jobId = string.Empty;
            var expireIn = new TimeSpan(1, 0, 0);
            var instance = CreateTransaction();

            Assert.Throws<ArgumentException>(nameof(jobId),
                () => instance.ExpireJob(jobId, expireIn));
        }

        [Fact]
        public void ExpireJob_Throws_WhenTransactionDisposed()
        {
            string jobId = "1";
            var expireIn = new TimeSpan(1, 0, 0);

            AssertThrowsObjectDisposed(instance => instance.ExpireJob(jobId, expireIn));
        }

        [Fact]
        public void ExpireJob_SetsJobExpirationData()
        {
            var job = InsertJob();
            var anotherJob = InsertJob();
            var jobId = job.Id.ToString(CultureInfo.InvariantCulture);
            var expireIn = new TimeSpan(1, 0, 0);
            var expiredFrom = DateTime.UtcNow + expireIn;

            UseTransaction(true, instance => instance.ExpireJob(jobId, expireIn));

            var expiredTo = DateTime.UtcNow + expireIn;
            UseContext(context =>
            {
                var actualJob = context.Jobs.Single(x => x.Id == job.Id);
                Assert.True(expiredFrom <= actualJob.ExpireAt);
                Assert.True(expiredTo >= actualJob.ExpireAt);
                anotherJob = actualJob = context.Jobs.Single(x => x.Id == anotherJob.Id);
                Assert.Null(anotherJob.ExpireAt);
            });
        }

        [Fact]
        public void ExpireHash_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var expireIn = new TimeSpan(1, 0, 0);
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.ExpireHash(key, expireIn));
        }

        [Fact]
        public void ExpireHash_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);

            AssertThrowsObjectDisposed(instance => instance.ExpireHash(key, expireIn));
        }

        [Fact]
        public void ExpireHash_SetsHashExpirationData()
        {
            var hashes = new[]
            {
                new HangfireHash
                {
                    Key = "hash-1",
                    Field = "field",
                },
                new HangfireHash
                {
                    Key = "hash-2",
                    Field = "field",
                },
            };

            UseContextSavingChanges(context => context.AddRange(hashes));
            var expireIn = new TimeSpan(1, 0, 0);
            var expiredFrom = DateTime.UtcNow + expireIn;

            UseTransaction(true, instance => instance.ExpireHash("hash-1", new TimeSpan(1, 0, 0)));

            var expiredTo = DateTime.UtcNow + expireIn;
            UseContext(context =>
            {
                var records = context.Hashes.ToDictionary(x => x.Key, x => x.ExpireAt);
                Assert.True(expiredFrom < records["hash-1"]);
                Assert.True(records["hash-1"] < expiredTo);
                Assert.Null(records["hash-2"]);
            });
        }

        [Fact]
        public void ExpireList_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var expireIn = new TimeSpan(1, 0, 0);
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.ExpireList(key, expireIn));
        }

        [Fact]
        public void ExpireList_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);

            AssertThrowsObjectDisposed(instance => instance.ExpireList(key, expireIn));
        }

        [Fact]
        public void ExpireList_SetsExpirationTime()
        {
            var lists = new[]
            {
                new HangfireList
                {
                    Key = "list-1",
                    Value = "1",
                },
                new HangfireList
                {
                    Key = "list-2",
                    Value = "1",
                },
            };
            UseContextSavingChanges(context => context.AddRange(lists));
            var expireIn = new TimeSpan(1, 0, 0);
            var expiredFrom = DateTime.UtcNow + expireIn;

            UseTransaction(true, instance => instance.ExpireList("list-1", new TimeSpan(1, 0, 0)));

            var expiredTo = DateTime.UtcNow + expireIn;
            UseContext(context =>
            {
                var records = context.Lists.ToDictionary(x => x.Key, x => x.ExpireAt);
                Assert.True(expiredFrom < records["list-1"]);
                Assert.True(records["list-1"] < expiredTo);
                Assert.Null(records["list-2"]);
            });
        }

        [Fact]
        public void ExpireSet_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var expireIn = new TimeSpan(1, 0, 0);
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.ExpireSet(key, expireIn));
        }

        [Fact]
        public void ExpireSet_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);

            AssertThrowsObjectDisposed(instance => instance.ExpireSet(key, expireIn));
        }

        [Fact]
        public void ExpireSet_SetsExpirationTime_OnASet_WithGivenKey()
        {
            var sets = new[]
            {
                new HangfireSet
                {
                    Key = "set-1",
                    Value = "1",
                },
                new HangfireSet
                {
                    Key = "set-2",
                    Value = "1",
                },
            };

            UseContextSavingChanges(context => context.AddRange(sets));
            var expireIn = new TimeSpan(1, 0, 0);
            var expiredFrom = DateTime.UtcNow + expireIn;

            UseTransaction(true, instance => instance.ExpireSet("set-1", expireIn));

            var expiredTo = DateTime.UtcNow + expireIn;
            UseContext(context =>
            {
                var records = context.Sets.ToDictionary(x => x.Key, x => x.ExpireAt);
                Assert.True(expiredFrom < records["set-1"]);
                Assert.True(records["set-1"] < expiredTo);
                Assert.Null(records["set-2"]);
            });
        }

        [Fact]
        public void IncrementCounter_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var expireIn = new TimeSpan(1, 0, 0);
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.IncrementCounter(key));
            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.IncrementCounter(key, expireIn));
        }

        [Fact]
        public void IncrementCounter_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);

            AssertThrowsObjectDisposed(instance => instance.IncrementCounter(key));
            AssertThrowsObjectDisposed(instance => instance.IncrementCounter(key, expireIn));
        }

        [Fact]
        public void IncrementCounter_AddsRecordToCounterTable_WithoutExpiration()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);

            UseTransaction(true, instance => instance.IncrementCounter(key));

            UseContext(context =>
            {
                var record = context.Counters.Single();
                Assert.Equal(key, record.Key);
                Assert.Equal(1L, record.Value);
                Assert.Null(record.ExpireAt);
            });
        }

        [Fact]
        public void IncrementCounter_AddsRecordToCounterTable_WithExpiration()
        {
            string key = "key";
            var expireIn = new TimeSpan(1, 0, 0);
            var expiredFrom = DateTime.UtcNow + expireIn;

            UseTransaction(true, instance => instance.IncrementCounter(key, expireIn));

            var expiredTo = DateTime.UtcNow + expireIn;
            UseContext(context =>
            {
                var record = context.Counters.Single();
                Assert.Equal(key, record.Key);
                Assert.Equal(1L, record.Value);
                Assert.NotNull(record.ExpireAt);
                Assert.True(expiredFrom <= record.ExpireAt);
                Assert.True(expiredTo >= record.ExpireAt);
            });
        }

        [Fact]
        public void InsertToList_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            string value = "value";
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.InsertToList(key, value));
        }

        [Fact]
        public void InsertToList_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            string value = "value";

            AssertThrowsObjectDisposed(instance => instance.InsertToList(key, value));
        }

        [Fact]
        public void InsertToList_AddsARecord_WithGivenValues()
        {
            string key = "key";

            UseTransaction(true, instance => instance.InsertToList(key, "my-value"));

            UseContext(context =>
            {
                var record = context.Lists.Single();
                Assert.Equal(key, record.Key);
                Assert.Equal("my-value", record.Value);
            });
        }

        [Fact]
        public void InsertToList_AddsAnotherRecord_WhenBothKeyAndValueAreExist()
        {
            string key = "key";

            UseTransaction(true, instance =>
            {
                instance.InsertToList(key, "my-value");
                instance.InsertToList(key, "my-value");
            });

            UseContext(context => Assert.Equal(2, context.Lists.Count()));
        }

        [Fact]
        public void PersistJob_Throws_WhenJobIdParameterIsNull()
        {
            string jobId = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(jobId),
                () => instance.PersistJob(jobId));
        }

        [Fact]
        public void PersistJob_Throws_WhenJobIdParameterIsEmpty()
        {
            string jobId = string.Empty;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentException>(nameof(jobId),
                () => instance.PersistJob(jobId));
        }

        [Fact]
        public void PersistJob_Throws_WhenTransactionDisposed()
        {
            string jobId = "1";

            AssertThrowsObjectDisposed(instance => instance.PersistJob(jobId));
        }

        [Fact]
        public void PersistJob_ClearsTheJobExpirationData()
        {
            var now = DateTime.UtcNow;
            var job = InsertJob(now);
            var anotherJob = InsertJob(now);
            var jobId = job.Id.ToString(CultureInfo.InvariantCulture);
            var expireIn = new TimeSpan(1, 0, 0);

            UseTransaction(true, instance => instance.PersistJob(jobId));

            UseContext(context =>
            {
                var actualJob = context.Jobs.Single(x => x.Id == job.Id);
                Assert.Null(actualJob.ExpireAt);
                anotherJob = actualJob = context.Jobs.Single(x => x.Id == anotherJob.Id);
                Assert.Equal(now, anotherJob.ExpireAt);
            });
        }

        [Fact]
        public void PersistHash_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.PersistHash(key));
        }

        [Fact]
        public void PersistHash_Throws_WhenTransactionDisposed()
        {
            string key = "key";

            AssertThrowsObjectDisposed(instance => instance.PersistHash(key));
        }

        [Fact]
        public void PersistHash_ClearsExpirationTime()
        {
            var expiredAt = DateTime.UtcNow + new TimeSpan(1, 0, 0);
            var hashes = new[]
            {
                new HangfireHash
                {
                    Key = "hash-1",
                    Field = "field",
                    ExpireAt = expiredAt,
                },
                new HangfireHash
                {
                    Key = "hash-2",
                    Field = "field",
                    ExpireAt = expiredAt,
                },
            };
            UseContextSavingChanges(context => context.AddRange(hashes));

            UseTransaction(true, instance => instance.PersistHash("hash-1"));

            UseContext(context =>
            {
                var records = context.Hashes.ToDictionary(x => x.Key, x => x.ExpireAt);
                Assert.Null(records["hash-1"]);
                Assert.Equal(expiredAt, records["hash-2"]);
            });
        }

        [Fact]
        public void PersistList_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.PersistList(key));
        }

        [Fact]
        public void PersistList_Throws_WhenTransactionDisposed()
        {
            string key = "key";

            AssertThrowsObjectDisposed(instance => instance.PersistList(key));
        }

        [Fact]
        public void PersistList_ClearsExpirationTime()
        {
            var expireAt = DateTime.UtcNow + new TimeSpan(1, 0, 0);
            var lists = new[]
            {
                new HangfireList
                {
                    Key = "list-1",
                    Value = "1",
                    Position = 0,
                    ExpireAt = expireAt,
                },
                new HangfireList
                {
                    Key = "list-2",
                    Value = "1",
                    Position = 0,
                    ExpireAt = expireAt,
                },
            };
            UseContextSavingChanges(context => context.AddRange(lists));

            UseTransaction(true, instance => instance.PersistList("list-1"));

            UseContext(context =>
            {
                var records = context.Lists.ToDictionary(x => x.Key, x => x.ExpireAt);
                Assert.Null(records["list-1"]);
                Assert.Equal(expireAt, records["list-2"]);
            });
        }

        [Fact]
        public void PersistSet_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.PersistSet(key));
        }

        [Fact]
        public void PersistSet_Throws_WhenTransactionDisposed()
        {
            string key = "key";

            AssertThrowsObjectDisposed(instance => instance.PersistSet(key));
        }

        [Fact]
        public void PersistSet_ClearsExpirationTime()
        {
            var expiredAt = DateTime.UtcNow + new TimeSpan(1, 0, 0);
            var sets = new[]
            {
                new HangfireSet
                {
                    Key = "set-1",
                    Value = "1",
                    ExpireAt = expiredAt,
                },
                new HangfireSet
                {
                    Key = "set-2",
                    Value = "1",
                    ExpireAt = expiredAt,
                },
            };

            UseContextSavingChanges(context => context.AddRange(sets));

            UseTransaction(true, instance => instance.PersistSet("set-1"));

            UseContext(context =>
            {
                var records = context.Sets.ToDictionary(x => x.Key, x => x.ExpireAt);
                Assert.Null(records["set-1"]);
                Assert.NotNull(records["set-2"]);
            });
        }

        [Fact]
        public void RemoveFromList_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            string value = "value";
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.RemoveFromList(key, value));
        }

        [Fact]
        public void RemoveFromList_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            string value = "value";

            AssertThrowsObjectDisposed(instance => instance.RemoveFromList(key, value));
        }

        [Fact]
        public void RemoveFromList_RemovesAllRecords_WithGivenKeyAndValue()
        {
            string key = "key";
            UseContextSavingChanges(context => context.AddRange(new[]
            {
                new HangfireList
                {
                    Key = key,
                    Value = "my-value",
                    Position = 0,
                },
                new HangfireList
                {
                    Key = key,
                    Value = "my-value",
                    Position = 1,
                },
            }));

            UseTransaction(true, instance => instance.RemoveFromList(key, "my-value"));

            UseContext(context => Assert.Empty(context.Lists));
        }

        [Fact]
        public void RemoveFromList_DoesNotRemoveRecords_WithSameKey_ButDifferentValue()
        {
            string key = "key";
            UseContextSavingChanges(context => context.AddRange(new[]
            {
                new HangfireList
                {
                    Key = key,
                    Value = "my-value",
                    Position = 0,
                },
            }));

            UseTransaction(true, instance => instance.RemoveFromList(key, "different-value"));

            UseContext(context => Assert.Single(context.Lists));
        }

        [Fact]
        public void RemoveFromList_DoesNotRemoveRecords_WithSameValue_ButDifferentKey()
        {
            string key = "key";
            UseContextSavingChanges(context => context.AddRange(new[]
            {
                new HangfireList
                {
                    Key = key,
                    Value = "my-value",
                    Position = 0,
                },
            }));

            UseTransaction(true, instance => instance.RemoveFromList("different-key", "my-value"));

            UseContext(context => Assert.Single(context.Lists));
        }

        [Fact]
        public void RemoveFromSet_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            string value = "value";
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.RemoveFromSet(key, value));
        }

        [Fact]
        public void RemoveFromSet_Throws_WhenValueParameterIsNull()
        {
            string key = "key";
            string value = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(value),
                () => instance.RemoveFromSet(key, value));
        }

        [Fact]
        public void RemoveFromSet_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            string value = "value";

            AssertThrowsObjectDisposed(instance => instance.RemoveFromSet(key, value));
        }

        [Fact]
        public void RemoveFromSet_RemovesARecord_WithGivenKeyAndValue()
        {
            string key = "key";
            UseContextSavingChanges(context => context.Add(new HangfireSet
            {
                Key = key,
                Value = "my-value",
            }));

            UseTransaction(true, instance => instance.RemoveFromSet(key, "my-value"));

            UseContext(context => Assert.Empty(context.Sets));
        }

        [Fact]
        public void RemoveFromSet_DoesNotRemoveRecord_WithSameKey_AndDifferentValue()
        {
            string key = "key";
            UseContextSavingChanges(context => context.Add(new HangfireSet
            {
                Key = key,
                Value = "my-value",
            }));

            UseTransaction(true, instance => instance.RemoveFromSet(key, "another-value"));

            UseContext(context => Assert.Single(context.Sets));
        }

        [Fact]
        public void RemoveFromSet_DoesNotRemoveRecord_WithSameValue_AndDifferentKey()
        {
            string key = "key";
            UseContextSavingChanges(context => context.Add(new HangfireSet
            {
                Key = key,
                Value = "my-value",
            }));

            UseTransaction(true, instance => instance.RemoveFromSet("another-key", "my-value"));

            UseContext(context => Assert.Single(context.Sets));
        }

        [Fact]
        public void RemoveHash_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.RemoveHash(key));
        }

        [Fact]
        public void RemoveHash_Throws_WhenTransactionDisposed()
        {
            string key = "key";

            AssertThrowsObjectDisposed(instance => instance.RemoveHash(key));
        }

        [Fact]
        public void RemoveHash_RemovesAllHashRecords()
        {
            string key = "key";
            var hashes = new[]
            {
                new HangfireHash
                {
                    Key = key,
                    Field = "field-1",
                    Value = "value-1",
                },
                new HangfireHash
                {
                    Key = key,
                    Field = "field-2",
                    Value = "value-2",
                },
            };
            UseContextSavingChanges(context => context.AddRange(hashes));

            UseTransaction(true, instance => instance.RemoveHash(key));

            UseContext(context => Assert.Empty(context.Hashes));
        }

        [Fact]
        public void RemoveSet_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.RemoveSet(key));
        }

        [Fact]
        public void RemoveSet_Throws_WhenTransactionDisposed()
        {
            string key = "key";

            AssertThrowsObjectDisposed(instance => instance.RemoveSet(key));
        }

        [Fact]
        public void RemoveSet_RemovesASet()
        {
            var sets = new[]
            {
                new HangfireSet
                {
                    Key = "set-1",
                    Value = "1",
                    CreatedAt = DateTime.UtcNow,
                },
                new HangfireSet
                {
                    Key = "set-2",
                    Value = "1",
                    CreatedAt = DateTime.UtcNow,
                },
            };
            UseContextSavingChanges(context => context.AddRange(sets));

            UseTransaction(true, instance => instance.RemoveSet("set-1"));

            UseContext(context =>
            {
                var record = context.Sets.Single();
                Assert.Equal("set-2", record.Key);
            });
        }

        [Fact]
        public void SetJobState_Throws_WhenJobIdParameterIsNull()
        {
            string jobId = null;
            var state = new Mock<IState>().Object;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(jobId),
                () => instance.SetJobState(jobId, state));
        }

        [Fact]
        public void SetJobState_Throws_WhenJobIdParameterIsEmpty()
        {
            string jobId = string.Empty;
            var state = new Mock<IState>().Object;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentException>(nameof(jobId),
                () => instance.SetJobState(jobId, state));
        }

        [Fact]
        public void SetJobState_Throws_WhenStateParameterIsNull()
        {
            string jobId = "1";
            IState state = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(state),
                () => instance.SetJobState(jobId, state));
        }

        [Fact]
        public void SetJobState_Throws_WhenTransactionDisposed()
        {
            string jobId = "1";
            var state = new Mock<IState>().Object;

            AssertThrowsObjectDisposed(instance => instance.SetJobState(jobId, state));
        }

        [Fact]
        public void SetJobState_AppendsAStateAndSetItToTheJob()
        {
            var createdAtFrom = DateTime.UtcNow;
            var job = InsertJob();
            var anotherJob = InsertJob();
            var state = new Mock<IState>();
            state.Setup(x => x.Name).Returns("State");
            state.Setup(x => x.Reason).Returns("Reason");
            state.Setup(x => x.SerializeData()).
                Returns(new Dictionary<string, string>
                {
                    ["Name"] = "Value",
                });
            var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

            UseTransaction(true, instance => instance.SetJobState(jobId, state.Object));

            var createdAtTo = DateTime.UtcNow;
            UseContext(context =>
            {
                var actualJobState = Assert.Single(context.JobStates);
                Assert.Equal("State", actualJobState.Name);
                var actualState = Assert.Single(context.States);
                Assert.Equal("State", actualState.Name);
                Assert.Equal("Reason", actualState.Reason);
                Assert.True(createdAtFrom <= actualState.CreatedAt);
                Assert.True(actualState.CreatedAt <= createdAtTo);
                var data = actualState.Data;
                Assert.Single(data);
                Assert.Equal("Value", data["Name"]);
                Assert.Equal(actualState.Id, actualJobState.StateId);
            });
        }

        [Fact]
        public void SetRangeInHash_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            var keyValuePairs = new Dictionary<string, string>();
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.SetRangeInHash(key, keyValuePairs));
        }

        [Fact]
        public void SetRangeInHash_Throws_WhenKeyValuePairsParameterIsNull()
        {
            string key = "key";
            Dictionary<string, string> keyValuePairs = null;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(keyValuePairs),
                () => instance.SetRangeInHash(key, keyValuePairs));
        }

        [Fact]
        public void SetRangeInHash_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            var keyValuePairs = new Dictionary<string, string>();

            AssertThrowsObjectDisposed(instance => instance.SetRangeInHash(key, keyValuePairs));
        }

        [Fact]
        public void SetRangeInHash_MergesAllRecords()
        {
            string key = "key";
            var keyValuePairs = new Dictionary<string, string>
            {
                ["field-1"] = "value-1",
                ["field-2"] = "value-2",
            };

            UseTransaction(true, instance => instance.SetRangeInHash(key, keyValuePairs));

            UseContext(context =>
            {
                var result = context.Hashes.
                    Where(x => x.Key == key).
                    ToDictionary(x => x.Field, x => x.Value);
                Assert.Equal("value-1", result["field-1"]);
                Assert.Equal("value-2", result["field-2"]);
            });
        }

        [Fact]
        public void TrimList_Throws_WhenKeyParameterIsNull()
        {
            string key = null;
            const int keepStartingFrom = 0;
            const int keepEndingAt = 1;
            var instance = CreateTransaction();

            Assert.Throws<ArgumentNullException>(nameof(key),
                () => instance.TrimList(key, keepStartingFrom, keepEndingAt));
        }

        [Fact]
        public void TrimList_Throws_WhenTransactionDisposed()
        {
            string key = "key";
            const int keepStartingFrom = 0;
            const int keepEndingAt = 1;

            AssertThrowsObjectDisposed(
                instance => instance.TrimList(key, keepStartingFrom, keepEndingAt));
        }

        [Fact]
        public void TrimList_TrimsAList_ToASpecifiedRange()
        {
            string key = "key";
            UseContextSavingChanges(context => context.AddRange(new[]
            {
                new HangfireList
                {
                    Key = key,
                    Position = 0,
                    Value = "0",
                },
                new HangfireList
                {
                    Key = key,
                    Position = 1,
                    Value = "1",
                },
                new HangfireList
                {
                    Key = key,
                    Position = 2,
                    Value = "2",
                },
                new HangfireList
                {
                    Key = key,
                    Position = 3,
                    Value = "3",
                },
            }));

            UseTransaction(true, instance => instance.TrimList(key, 1, 2));

            UseContext(context =>
            {
                var records = context.Lists.ToArray();
                Assert.Equal(2, records.Length);
                Assert.Equal("1", records[0].Value);
                Assert.Equal("2", records[1].Value);
            });
        }

        [Fact]
        public void TrimList_RemovesRecordsToEnd_IfKeepAndingAt_GreaterThanMaxElementIndex()
        {
            string key = "key";
            UseContextSavingChanges(context => context.AddRange(new[]
            {
                new HangfireList
                {
                    Key = key,
                    Position = 0,
                    Value = "0",
                },
                new HangfireList
                {
                    Key = key,
                    Position = 1,
                    Value = "1",
                },
                new HangfireList
                {
                    Key = key,
                    Position = 2,
                    Value = "2",
                },
            }));

            UseTransaction(true, instance => instance.TrimList(key, 1, 100));

            UseContext(context =>
            {
                var recordCount = context.Lists.Count();
                Assert.Equal(2, recordCount);
            });
        }

        [Fact]
        public void TrimList_RemovesAllRecords_WhenStartingFromValue_GreaterThanMaxElementIndex()
        {
            string key = "key";
            UseContextSavingChanges(context => context.Lists.
                Add(new HangfireList
                {
                    Key = key,
                    Position = 0,
                    Value = "0",
                }));

            UseTransaction(true, instance => instance.TrimList(key, 1, 100));

            UseContext(context =>
            {
                var recordCount = context.Lists.Count();
                Assert.Equal(0, recordCount);
            });
        }

        [Fact]
        public void TrimList_RemovesAllRecords_IfStartFromGreaterThanEndingAt()
        {
            string key = "key";
            UseContextSavingChanges(context => context.Lists.
                Add(new HangfireList
                {
                    Key = key,
                    Position = 0,
                    Value = "0",
                }));

            UseTransaction(true, instance => instance.TrimList(key, 1, 0));

            UseContext(context =>
            {
                var recordCount = context.Lists.Count();
                Assert.Equal(0, recordCount);
            });
        }

        [Fact]
        public void TrimList_RemovesRecords_OnlyOfAGivenKey()
        {
            string key = "key";
            string anotherKey = "another-key";
            UseContextSavingChanges(context => context.Lists.
                Add(new HangfireList
                {
                    Key = key,
                    Position = 0,
                    Value = "0",
                }));

            UseTransaction(true, instance => instance.TrimList(anotherKey, 1, 0));

            UseContext(context =>
            {
                var recordCount = context.Lists.Count();
                Assert.Equal(1, recordCount);
            });
        }

        private void AssertThrowsObjectDisposed(
            Action<EntityFrameworkCoreJobStorageTransaction> action)
        {
            var options = new DbContextOptions<HangfireContext>();
            var instance = CreateTransaction();
            instance.Dispose();
            var exception = Assert.Throws<ObjectDisposedException>(() => action(instance));
            Assert.Equal(instance.GetType().FullName, exception.ObjectName);
        }

        private EntityFrameworkCoreJobStorageTransaction CreateTransaction()
        {
            var queueProvider = new Mock<IPersistentJobQueueProvider>().Object;
            return new EntityFrameworkCoreJobStorageTransaction(Options, queueProvider);
        }

        private HangfireJob InsertJob(DateTime? expireAt = null)
        {
            var job = new HangfireJob
            {
                InvocationData = new InvocationData(null, null, null, string.Empty),
                CreatedAt = DateTime.UtcNow,
                ExpireAt = expireAt,
            };
            UseContextSavingChanges(context => context.Add(job));

            return job;
        }

        private void UseTransaction(
            bool commit,
            Action<EntityFrameworkCoreJobStorageTransaction> action)
        {
            using (var transaction = CreateTransaction())
            {
                action(transaction);
                if (commit)
                    transaction.Commit();
            }
        }
    }
}
