using System;
using System.Linq;
using System.Threading;
using Hangfire.Logging;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
#pragma warning disable CS0618
    internal class CountersAggregator : IServerComponent
#pragma warning restore CS0618
    {
        private readonly ILog _logger = LogProvider.For<CountersAggregator>();
        private readonly EFCoreStorage _storage;

        public CountersAggregator(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _logger.Debug("Aggregating records in '"+ nameof(HangfireCounter) +"' table...");

            int removedCount;
            do
            {
                removedCount = 0;
                using (var context = _storage.CreateContext())
                {
                    var key = (
                        from counter in context.Counters
                        group counter by counter.Key into @group
                        let count = @group.Count()
                        where count > 1
                        orderby count descending
                        select @group.Key).
                        FirstOrDefault();

                    if (key != null)
                    {
                        var itemsToRemove = (
                            from counter in context.Counters
                            where counter.Key == key
                            select counter).
                            Take(100).
                            ToArray();

                        if (itemsToRemove.Length > 1)
                        {
                            context.Counters.RemoveRange(itemsToRemove);
                            context.Counters.Add(new HangfireCounter
                            {
                                Key = key,
                                Value = itemsToRemove.Sum(x => x.Value),
                                ExpireAt = itemsToRemove.Max(x => x.ExpireAt),
                            });
                            removedCount += itemsToRemove.Length;

                            try
                            {
                                context.SaveChanges();
                            }
                            catch (DbUpdateConcurrencyException)
                            {
                                // Someone else has removed at least one record. Database wins.
                                continue;
                            }
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            while (removedCount > 0);

            _logger.Debug("Records from the '" + nameof(HangfireCounter) + "' table aggregated.");

            cancellationToken.WaitHandle.WaitOne(_storage.CountersAggregationInterval);
        }
    }
}
