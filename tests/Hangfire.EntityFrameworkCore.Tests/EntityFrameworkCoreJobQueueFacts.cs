using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EntityFrameworkCoreJobQueueFacts : HangfireContextTest
    {
        [Fact]
        public void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new EntityFrameworkCoreJobQueue(options));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var options = new DbContextOptions<HangfireContext>();

            var instance = new EntityFrameworkCoreJobQueue(options);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    instance.GetFieldValue("_options")));
        }

        [Fact]
        public void Dequeue_Throws_WhenQueuesParameterIsNull()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            string[] queues = null;

            Assert.Throws<ArgumentNullException>(nameof(queues),
                () => instance.Dequeue(queues, CancellationToken.None));
        }

        [Fact]
        public void Dequeue_Throws_WhenQueuesParameterIsEmpty()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            string[] queues = Array.Empty<string>();

            Assert.Throws<ArgumentException>(nameof(queues),
                () => instance.Dequeue(queues, CancellationToken.None));
        }

        [Fact]
        public void Dequeue_Throws_WhenCancellationTokenIsSet()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            string[] queues = { "default" };
            var source = new CancellationTokenSource(0);

            Assert.Throws<OperationCanceledException>(
                () => instance.Dequeue(queues, source.Token));
        }

        [Fact]
        public void Dequeue_FetchesJob_FromTheSpecifiedQueue()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            string queue = "queue";
            var job = new HangfireJob
            {
                ClrType = "type",
                Method = "method",
                Queues = new List<HangfireJobQueue>
                {
                    new HangfireJobQueue
                    {
                        Queue = queue,
                    },
                },
            };
            UseContextSavingChanges(context => context.Add(job));

            var result = instance.Dequeue(new[] { queue }, CancellationToken.None);

            Assert.NotNull(result);
            var fetchedJob = Assert.IsType<EntityFrameworkCoreFetchedJob>(result);
            Assert.Equal(job.Queues.First().Id, fetchedJob.Id);
            Assert.Equal(queue, fetchedJob.Queue);
            Assert.Equal(job.Id, fetchedJob.JobId);
            UseContext(context =>
            {
                var queueItem = Assert.Single(context.JobQueues);
                Assert.Equal(fetchedJob.Id, queueItem.Id);
                Assert.Equal(fetchedJob.Queue, queueItem.Queue);
                Assert.Equal(fetchedJob.JobId, queueItem.JobId);
                Assert.NotNull(queueItem.FetchedAt);
            });
        }

        [Fact]
        public void Dequeue_Throws_WhenThereAreNoJobs()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            string queue = "queue";
            var source = new CancellationTokenSource(50);

            Assert.Throws<OperationCanceledException>(
                () => instance.Dequeue(new[] { queue }, source.Token));
        }

        [Fact]
        public void Enqueue_Throws_WhenQueueParameterIsNull()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            string queue = null;
            var jobId = "1";

            Assert.Throws<ArgumentNullException>(nameof(queue),
                () => instance.Enqueue(queue, jobId));
        }

        [Fact]
        public void Enqueue_Throws_WhenQueueParameterIsEmpty()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            string queue = string.Empty;
            var jobId = "1";

            Assert.Throws<ArgumentException>(nameof(queue),
                () => instance.Enqueue(queue, jobId));
        }

        [Fact]
        public void Enqueue_Throws_WhenJobIdParameterIsNull()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            var queue = "queue";
            string jobId = null;

            Assert.Throws<ArgumentNullException>(nameof(jobId),
                () => instance.Enqueue(queue, jobId));
        }

        [Fact]
        public void Enqueue_Throws_WhenJobIdParameterIsInvalid()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            var queue = "queue";
            string jobId = "invalid";

            Assert.Throws<FormatException>(
                () => instance.Enqueue(queue, jobId));
        }

        [Fact]
        public void Enqueue_CompletesSuccessfully_WhenJobExists()
        {
            var job = new HangfireJob
            {
                ClrType = "type",
                Method = "method",
            };
            UseContextSavingChanges(context => context.Add(job));
            var instance = new EntityFrameworkCoreJobQueue(Options);
            var queue = "queue";

            instance.Enqueue(queue, job.Id.ToString(CultureInfo.InvariantCulture));

            UseContext(context =>
            {
                var actual = Assert.Single(context.JobQueues);
                Assert.Equal(job.Id, actual.JobId);
                Assert.Equal(queue, actual.Queue);
                Assert.Null(actual.FetchedAt);
            });
        }

        [Fact]
        public void Enqueue_Throws_WhenJobNotExists()
        {
            var instance = new EntityFrameworkCoreJobQueue(Options);
            var queue = "queue";

            Assert.Throws<InvalidOperationException>(() => instance.Enqueue(queue, "1"));
            
            UseContext(context => Assert.Empty(context.JobQueues));
        }
    }
}
