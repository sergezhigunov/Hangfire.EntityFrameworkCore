using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreJobQueue : IPersistentJobQueue
    {
        private readonly EFCoreStorage _storage;

        internal static AutoResetEvent NewItemInQueueEvent { get; } = new AutoResetEvent(true);

        public EFCoreJobQueue([NotNull] EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IFetchedJob Dequeue([NotNull] string[] queues, CancellationToken cancellationToken)
        {
            if (queues == null)
                throw new ArgumentNullException(nameof(queues));
            if (queues.Length == 0)
                throw new ArgumentException(null, nameof(queues));

            var waitHandles = new[]
            {
                cancellationToken.WaitHandle,
                NewItemInQueueEvent,
            };

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var context = _storage.CreateContext())
                {
                    var queueItem = (
                        from item in context.Set<HangfireQueuedJob>()
                        where queues.Contains(item.Queue)
                        where item.FetchedAt == null
                        orderby item.Id ascending
                        select item).
                        FirstOrDefault();

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
                throw new ArgumentException(null, nameof(queue));
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
                    throw new InvalidOperationException(null, exception);
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
