using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EntityFrameworkCoreJobQueueMonitoringApiFacts : HangfireContextTest
    {
        [Fact]
        public void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new EntityFrameworkCoreJobQueueMonitoringApi(options));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var options = new DbContextOptions<HangfireContext>();

            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(options);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    instance.GetFieldValue("_options")));
        }

        [Fact]
        public void GetEnqueuedJobIds_Throws_IfQueueParameterIsNull()
        {
            string queue = null;
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            Assert.Throws<ArgumentNullException>(nameof(queue),
                () => instance.GetEnqueuedJobIds(queue, 0, 1));
        }

        [Fact]
        public void GetEnqueuedJobIds_ReturnsEmptyCollection_IfQueueIsEmpty()
        {
            string queue = "queue";
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var result = instance.GetEnqueuedJobIds(queue, 5, 15);

            Assert.Empty(result);
        }

        [Fact]
        public void GetEnqueuedJobIds_ReturnsCorrectResult()
        {
            string queue = "queue";
            var jobs = Enumerable.Repeat(0, 10).
                Select(_ => new HangfireJob
                {
                    ClrType = "test",
                    Method = "test",
                    Queues = new List<HangfireJobQueue>
                    {
                        new HangfireJobQueue
                        {
                            Queue = queue,
                        }
                    },
                }).
                ToArray();
            UseContextSavingChanges(context => context.AddRange(jobs));
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var result = instance.GetEnqueuedJobIds(queue, 3, 2).ToArray();

            Assert.Equal(2, result.Length);
            var jobIds = jobs.SelectMany(x => x.Queues).OrderBy(x => x.Id).
                Select(x => x.JobId.ToString(CultureInfo.InvariantCulture)).
                ToArray();
            Assert.Equal(jobIds[3], result[0]);
            Assert.Equal(jobIds[4], result[1]);
        }

        [Fact]
        public void GetFetchedJobIds_Throws_IfQueueParameterIsNull()
        {
            string queue = null;
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            Assert.Throws<ArgumentNullException>(nameof(queue),
                () => instance.GetFetchedJobIds(queue, 0, 1));
        }

        [Fact]
        public void GetFetchedJobIds_ReturnsEmptyCollection_IfQueueIsEmpty()
        {
            string queue = "queue";
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var result = instance.GetFetchedJobIds(queue, 5, 15);

            Assert.Empty(result);
        }

        [Fact]
        public void GetFetchedJobIds_ReturnsCorrectResult()
        {
            string queue = "queue";
            var jobs = Enumerable.Repeat(0, 10).
                Select(_ => new HangfireJob
                {
                    ClrType = "test",
                    Method = "test",
                    Queues = new List<HangfireJobQueue>
                    {
                        new HangfireJobQueue
                        {
                            Queue = queue,
                            FetchedAt = DateTime.UtcNow,
                        }
                    },
                }).
                ToArray();
            UseContextSavingChanges(context => context.AddRange(jobs));
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var result = instance.GetFetchedJobIds(queue, 3, 2).ToArray();

            Assert.Equal(2, result.Length);
            var jobIds = jobs.SelectMany(x => x.Queues).OrderBy(x => x.Id).
                Select(x => x.JobId.ToString(CultureInfo.InvariantCulture)).
                ToArray();
            Assert.Equal(jobIds[3], result[0]);
            Assert.Equal(jobIds[4], result[1]);
        }

        [Fact]
        public void GetQueues_ReturnsEmptyCollection_WhenQueuedItemsNotExisits()
        {
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var queues = instance.GetQueues();

            Assert.Empty(queues);
        }

        [Fact]
        public void GetQueues_ReturnsAllGivenQueues()
        {
            var date = DateTime.UtcNow;
            var queues = Enumerable.Repeat(0, 5).
                Select(x => Guid.NewGuid().ToString()).
                ToArray();
            var job = new HangfireJob
            {
                ClrType = "test",
                Method = "test",
                Queues = queues.
                    Select(x => new HangfireJobQueue
                    {
                        Queue = x,
                    }).
                    ToList()
            };
            UseContextSavingChanges(context => context.Jobs.Add(job));
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var result = instance.GetQueues();

            Assert.Equal(queues.OrderBy(x => x), result.OrderBy(x => x));
        }

        [Fact]
        public void GetQueueStatistics_Throws_whenQueueParametrIsNull()
        {
            string queue = null;
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            Assert.Throws<ArgumentNullException>(nameof(queue),
                () => instance.GetQueueStatistics(queue));
        }

        [Fact]
        public void GetQueueStatistics_ReturnsZeroes_WhenQueueIsEmpty()
        {
            string queue = "queue";
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var result = instance.GetQueueStatistics(queue);

            Assert.NotNull(result);
            Assert.Equal(default, result.Enqueued);
            Assert.Equal(default, result.Fetched);
        }

        [Fact]
        public void GetQueueStatistics_ReturnsCorrectResult_WhenQueueIsEmpty()
        {
            string queue = "queue";
            var jobs = Enumerable.Range(0, 5).
                Select(index => new HangfireJob
                {
                    ClrType = "test",
                    Method = "test",
                    Queues = new List<HangfireJobQueue>
                    {
                        new HangfireJobQueue
                        {
                            Queue = queue,
                            FetchedAt = index < 2 ? default(DateTime?) : DateTime.UtcNow,
                        }
                    },
                }).
                ToArray();
            UseContextSavingChanges(context => context.AddRange(jobs));
            var instance = new EntityFrameworkCoreJobQueueMonitoringApi(Options);

            var result = instance.GetQueueStatistics(queue);

            Assert.NotNull(result);
            Assert.Equal(2, result.Enqueued);
            Assert.Equal(3, result.Fetched);
        }
    }
}
