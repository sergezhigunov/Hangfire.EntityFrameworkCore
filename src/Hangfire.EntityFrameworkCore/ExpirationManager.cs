using System;
using System.Linq;
using System.Threading;
using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore
{
#pragma warning disable 618
    internal class ExpirationManager : IServerComponent
#pragma warning restore 618
    {
        private readonly EFCoreStorage _storage;

        public ExpirationManager(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _storage.UseContextSavingChanges(context =>
            {
                var now = DateTime.UtcNow;
                context.Counters.RemoveRange(
                    from counter in context.Counters
                    group counter by counter.Key into @group
                    where @group.Max(x => x.ExpireAt) < now
                    from counter in @group
                    select counter);

                context.Jobs.
                    RemoveRange(context.Jobs.Where(x => x.ExpireAt < now));

                context.Lists.
                    RemoveRange(context.Lists.Where(x => x.ExpireAt < now));

                context.Sets.
                    RemoveRange(context.Sets.Where(x => x.ExpireAt < now));

                context.Hashes.
                    RemoveRange(context.Hashes.Where(x => x.ExpireAt < now));
            });

            cancellationToken.WaitHandle.WaitOne(_storage.JobExpirationCheckInterval);
        }
    }
}
