using System;
using System.Collections.Concurrent;
using System.Threading;
using Hangfire.Common;
using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore
{
#pragma warning disable CS0618
    internal sealed class EFCoreHeartbeatProcess : IServerComponent, IBackgroundProcess
#pragma warning restore CS0618
    {
        private readonly ConcurrentDictionary<EFCoreFetchedJob, object> _items = new();

        public void Track(EFCoreFetchedJob item)
        {
            _items.TryAdd(item, null);
        }

        public void Untrack(EFCoreFetchedJob item)
        {
            _items.TryRemove(item, out var _);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            foreach (var item in _items)
            {
                item.Key.ExecuteKeepAliveQueryIfRequired();
            }

            cancellationToken.Wait(TimeSpan.FromSeconds(1));
        }

        public void Execute(BackgroundProcessContext context)
        {
            Execute(context.StoppingToken);
        }
    }
}
