using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Logging;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    using GetCountersToRemoveFunc = Func<HangfireContext, IEnumerable<HangfireCounter>>;

#pragma warning disable CS0618
    internal class CountersAggregator : IServerComponent
#pragma warning restore CS0618
    {
        private const int BatchSize = 1000;

        private static GetCountersToRemoveFunc GetCountersToRemoveFunc { get; } = EF.CompileQuery(
            (HangfireContext context) => (
                from x in context.Set<HangfireCounter>()
                where x.Key == (
                    from y in context.Set<HangfireCounter>()
                    group y by y.Key into g
                    let count = g.Count()
                    where count > 1
                    orderby count descending
                    select g.Key).
                    FirstOrDefault()
                select x).
                Take(BatchSize));

        private readonly ILog _logger = LogProvider.For<CountersAggregator>();
        private readonly EFCoreStorage _storage;

        public CountersAggregator(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _logger.DebugFormat(CoreStrings.CountersAggregatorExecuteStarting,
                nameof(HangfireCounter));
            int removedCount;
            do
            {
                removedCount = 0;
                using (var context = _storage.CreateContext())
                {
                    var counters = context.Set<HangfireCounter>();
                    var itemsToRemove = GetCountersToRemoveFunc(context). ToList();
                    var count = itemsToRemove.Count();
                    if (count > 1)
                    {
                        var key = itemsToRemove.First().Key;
                        context.RemoveRange(itemsToRemove);
                        context.Add(new HangfireCounter
                        {
                            Key = key,
                            Value = itemsToRemove.Sum(x => x.Value),
                            ExpireAt = itemsToRemove.Max(x => x.ExpireAt),
                        });
                        removedCount += count;

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
                cancellationToken.ThrowIfCancellationRequested();
            }
            while (removedCount > 0);

            _logger.TraceFormat(CoreStrings.CountersAggregatorExecuteCompleted,
                nameof(HangfireCounter));
            cancellationToken.WaitHandle.WaitOne(_storage.CountersAggregationInterval);
        }
    }
}
