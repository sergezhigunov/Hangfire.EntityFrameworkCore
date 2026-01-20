using System.Collections.Concurrent;
using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreHeartbeatProcess : IBackgroundProcess
    {
        private readonly ConcurrentDictionary<EFCoreFetchedJob, object> _items = new();

        public void Track(EFCoreFetchedJob item) => _items.TryAdd(item, null);

        public void Untrack(EFCoreFetchedJob item) => _items.TryRemove(item, out var _);

        public void Execute(BackgroundProcessContext context)
        {
            foreach (var item in _items)
                item.Key.ExecuteKeepAliveQueryIfRequired();
            context.StoppingToken.Wait(TimeSpan.FromSeconds(1));
        }
    }
}
