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
        private static readonly object s_lock = new object();
        private readonly DbContextOptions _options;
        private readonly TimeSpan _queuePollInterval = new TimeSpan(0, 0, 10);

        internal static AutoResetEvent NewItemInQueueEvent { get; } = new AutoResetEvent(true);

        public EFCoreJobQueue([NotNull] DbContextOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IFetchedJob Dequeue([NotNull] string[] queues, CancellationToken cancellationToken)
        {
            if (queues == null)
                throw new ArgumentNullException(nameof(queues));
            if (queues.Length == 0)
                throw new ArgumentException(null, nameof(queues));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (s_lock)
                    using (var context = _options.CreateContext())
                    {
                        var queueItem = (
                            from item in context.JobQueues
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
                                return new EFCoreFetchedJob(_options, queueItem);
                            }
                            catch (DbUpdateConcurrencyException)
                            {
                                continue;
                            }
                        }
                    }

                WaitHandle.WaitAny(
                    new[]
                    {
                        cancellationToken.WaitHandle,
                        NewItemInQueueEvent,
                    },
                    _queuePollInterval);
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

            var item = new HangfireJobQueue
            {
                JobId = long.Parse(jobId, CultureInfo.InvariantCulture),
                Queue = queue,
            };

            _options.UseContext(context =>
            {
                context.Add(item);
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
    }
}
