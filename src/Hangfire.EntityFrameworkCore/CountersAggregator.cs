using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore;

using GetCountersToRemoveFunc = Func<DbContext, IEnumerable<HangfireCounter>>;

internal class CountersAggregator : IBackgroundProcess
{
    private const int BatchSize = 1000;

    private static GetCountersToRemoveFunc GetCountersToRemoveFunc { get; } = EF.CompileQuery(
        (DbContext context) => (
            from x in context.Set<HangfireCounter>()
            where context.Set<HangfireCounter>().
                Any(y => y.Key == x.Key && y.Id != x.Id)
            orderby x.Key
            select x).
            Take(BatchSize));

    private readonly ILog _logger = LogProvider.For<CountersAggregator>();
    private readonly EFCoreStorage _storage;

    public CountersAggregator(EFCoreStorage storage)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(storage);
#else
        if (storage is null) throw new ArgumentNullException(nameof(storage));
#endif

        _storage = storage;
    }

    public void Execute([NotNull] BackgroundProcessContext context)
    {
        _logger.Debug(string.Format(null, CoreStrings.CountersAggregatorExecuteStarting, nameof(HangfireCounter)));
        int removedCount;
        do
        {
            removedCount = 0;
            using (var dbContext = _storage.CreateContext())
            {
                var lookup = GetCountersToRemoveFunc(dbContext).
                    ToLookup(x => x.Key);

                foreach (var items in lookup)
                {
                    var count = items.Count();
                    if (count > 1)
                    {
                        dbContext.RemoveRange(items);
                        dbContext.Add(new HangfireCounter
                        {
                            Key = items.Key,
                            Value = items.Sum(x => x.Value),
                            ExpireAt = items.Max(x => x.ExpireAt),
                        });
                        removedCount += count;
                    }
                }
                try
                {
                    dbContext.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else has removed at least one record. Database wins.
                    continue;
                }
            }
            context.StoppingToken.ThrowIfCancellationRequested();
        }
        while (removedCount > 0);

        _logger.Trace(string.Format(null, CoreStrings.CountersAggregatorExecuteCompleted, nameof(HangfireCounter)));
        context.StoppingToken.WaitHandle.WaitOne(_storage.CountersAggregationInterval);
    }
}
