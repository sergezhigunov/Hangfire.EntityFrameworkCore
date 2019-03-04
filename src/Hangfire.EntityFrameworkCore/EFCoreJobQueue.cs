using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    using DequeueFunc = Func<HangfireContext, string[], DateTime, HangfireQueuedJob>;

    internal sealed class EFCoreJobQueue : IPersistentJobQueue
    {
        private readonly EFCoreStorage _storage;

        internal static AutoResetEvent NewItemInQueueEvent { get; } = new AutoResetEvent(true);

        private static DequeueFunc DequeueFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string[] queues, DateTime expireAt) => (
                from x in context.Set<HangfireQueuedJob>()
                where queues.Contains(x.Queue)
                where
                    x.FetchedAt == null ||
                    x.FetchedAt < expireAt
                orderby x.Id ascending
                select x).
                FirstOrDefault());

        public EFCoreJobQueue([NotNull] EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IFetchedJob Dequeue([NotNull] string[] queues, CancellationToken cancellationToken)
        {
            if (queues == null)
                throw new ArgumentNullException(nameof(queues));
            if (queues.Length == 0)
                throw new ArgumentException(CoreStrings.ArgumentExceptionCollectionCannotBeEmpty,
                    nameof(queues));

            var waitHandles = new[]
            {
                cancellationToken.WaitHandle,
                NewItemInQueueEvent,
            };

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var expireAt = DateTime.UtcNow - _storage.SlidingInvisibilityTimeout;
                using (var context = _storage.CreateContext())
                {
                    var queueItem = DequeueFunc(context, queues, expireAt);
                    if (queueItem != null)
                    {
                        queueItem.FetchedAt = DateTime.UtcNow;
                        try
                        {
                            context.SaveChanges();
                            return new EFCoreFetchedJob(_storage, queueItem);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            continue;
                        }
                    }
                }

                WaitHandle.WaitAny(
                    waitHandles,
                    _storage.QueuePollInterval);
            }
        }

        public void Enqueue([NotNull] string queue, [NotNull] string jobId)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));
            if (queue.Length == 0)
                throw new ArgumentException(
                    CoreStrings.ArgumentExceptionCollectionCannotBeEmpty,
                    nameof(queue));
            if (jobId == null)
                throw new ArgumentNullException(nameof(jobId));

            var id = long.Parse(jobId, CultureInfo.InvariantCulture);

            _storage.UseContext(context =>
            {
                Enqueue(context, queue, id);
                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateException exception)
                {
                    throw new InvalidOperationException(
                        CoreStrings.InvalidOperationExceptionJobDoesNotExists,
                        exception);
                }
            });
        }

        internal void Enqueue(
            HangfireContext context,
            string queue,
            long jobId)
        {
            context.Add(new HangfireQueuedJob
            {
                JobId = jobId,
                Queue = queue,
            });
        }
    }
}
