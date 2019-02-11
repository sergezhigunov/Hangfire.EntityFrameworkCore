using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire.Storage;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EFCoreFetchedJobFacts : EFCoreStorageTest
    {
        [Fact]
        public void Ctor_Throws_WhenStorageParameterIsNull()
        {
            EFCoreStorage storage = null;
            var queuedJob = new HangfireQueuedJob();

            Assert.Throws<ArgumentNullException>(nameof(storage),
                () => new EFCoreFetchedJob(storage, queuedJob));
        }

        [Fact]
        public void Ctor_Throws_WhenItemParameterIsNull()
        {
            var storage = CreateStorageStub();
            HangfireQueuedJob queuedJob = null;

            Assert.Throws<ArgumentNullException>(nameof(queuedJob),
                () => new EFCoreFetchedJob(storage, queuedJob));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var queuedJob = new HangfireQueuedJob
            {
                Id = 1,
                JobId = 2,
                Queue = "queue",
            };

            using (var instance = new EFCoreFetchedJob(Storage, queuedJob))
            {
                Assert.False(Assert.IsType<bool>(
                    instance.GetFieldValue("_disposed")));
                Assert.False(Assert.IsType<bool>(
                    instance.GetFieldValue("_completed")));
                Assert.Same(Storage, Assert.IsType<EFCoreStorage>(
                    instance.GetFieldValue("_storage")));
                Assert.Same(queuedJob,
                    Assert.IsType<HangfireQueuedJob>(
                        instance.GetFieldValue("_queuedJob")));
                Assert.Equal(queuedJob.Id, instance.Id);
                Assert.Equal(queuedJob.JobId, instance.JobId);
                Assert.Equal(queuedJob.Queue, instance.Queue);
                Assert.Equal
                    (queuedJob.JobId.ToString(CultureInfo.InvariantCulture),
                    ((IFetchedJob)instance).JobId);
            }

        }

        [Fact]
        public void RemoveFromQueue_WhenItemAlreadyRemoved()
        {
            var item = new HangfireQueuedJob
            {
                Id = 1,
                Queue = "queue",
                FetchedAt = DateTime.UtcNow,
            };
            using (var instance = new EFCoreFetchedJob(Storage, item))
            {
                instance.RemoveFromQueue();

                UseContext(context => Assert.Empty(context.Set<HangfireQueuedJob>()));
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_completed")));
                Assert.False(Assert.IsType<bool>(
                    instance.GetFieldValue("_disposed")));
            }
        }

        [Fact]
        public void RemoveFromQueue_WhenItemExists()
        {
            var job = new HangfireJob
            {
                InvocationData = new InvocationData(null, null, null, string.Empty),
                QueuedJobs = new List<HangfireQueuedJob>
                {
                    new HangfireQueuedJob
                    {
                        Queue = "queue",
                        FetchedAt = DateTime.UtcNow,
                    },
                },
            };
            UseContextSavingChanges(context => context.Add(job));
            using (var instance = new EFCoreFetchedJob(Storage, job.QueuedJobs.Single()))
            {
                instance.RemoveFromQueue();

                UseContext(context =>
                {
                    Assert.Single(context.Set<HangfireJob>());
                    Assert.Empty(context.Set<HangfireQueuedJob>());
                });
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_completed")));
                Assert.False(Assert.IsType<bool>(
                    instance.GetFieldValue("_disposed")));
            }
        }

        [Fact]
        public void Requeue_CompletesSuccesfully_WhenItemAlreadyRemoved()
        {
            var queuedJob = new HangfireQueuedJob
            {
                Id = 1,
                JobId = 1,
                Queue = "queue",
                FetchedAt = DateTime.UtcNow,
            };
            using (var instance = new EFCoreFetchedJob(Storage, queuedJob))
            {
                instance.Requeue();

                UseContext(context => Assert.Empty(context.Set<HangfireQueuedJob>()));
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_completed")));
                Assert.False(Assert.IsType<bool>(
                    instance.GetFieldValue("_disposed")));
            }
        }

        [Fact]
        public void Requeue_CompletesSuccesfully_WhenItemExists()
        {
            var job = new HangfireJob
            {
                InvocationData = new InvocationData(null, null, null, string.Empty),
                QueuedJobs = new List<HangfireQueuedJob>
                {
                    new HangfireQueuedJob
                    {
                        Queue = "queue",
                        FetchedAt = DateTime.UtcNow,
                    },
                },
            };
            UseContextSavingChanges(context => context.Add(job));
            using (var instance = new EFCoreFetchedJob(Storage, job.QueuedJobs.Single()))
            {
                instance.Requeue();

                UseContext(context =>
                {
                    var item = Assert.Single(context.Set<HangfireQueuedJob>());
                    Assert.Null(item.FetchedAt);
                });
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_completed")));
                Assert.False(Assert.IsType<bool>(
                    instance.GetFieldValue("_disposed")));
            }
        }

        [Fact]
        public void Dispose_CompletesSuccesfully_WhenItemAlreadyRemoved()
        {
            var queuedJob = new HangfireQueuedJob
            {
                Id = 1,
                JobId = 1,
                Queue = "queue",
                FetchedAt = DateTime.UtcNow,
            };
            using (var instance = new EFCoreFetchedJob(Storage, queuedJob))
            {
                instance.Dispose();

                UseContext(context => Assert.Empty(context.Set<HangfireQueuedJob>()));
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_completed")));
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_disposed")));

                instance.Dispose();
            }
        }

        [Fact]
        public void Dispose_CompletesSuccesfully_WhenItemExists()
        {
            var job = new HangfireJob
            {
                InvocationData = new InvocationData(null, null, null, string.Empty),
                QueuedJobs = new List<HangfireQueuedJob>
                {
                    new HangfireQueuedJob
                    {
                        Queue = "queue",
                        FetchedAt = DateTime.UtcNow,
                    },
                },
            };
            UseContextSavingChanges(context => context.Add(job));
            using (var instance = new EFCoreFetchedJob(Storage, job.QueuedJobs.Single()))
            {
                instance.Dispose();

                UseContext(context =>
                {
                    var item = Assert.Single(context.Set<HangfireQueuedJob>());
                    Assert.Null(item.FetchedAt);
                });
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_completed")));
                Assert.True(Assert.IsType<bool>(
                    instance.GetFieldValue("_disposed")));

                instance.Dispose();
            }
        }
    }
}
