using System;
using System.Linq;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class EFCoreLockProvider : IDistributedLockProvider
    {
        private static readonly TimeSpan s_maxSleepDuration = new TimeSpan(0, 0, 1);
        private readonly DbContextOptions _options;
        private readonly TimeSpan _timeout;

        public EFCoreLockProvider(
            [NotNull] DbContextOptions options,
            TimeSpan timeout)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timeout = timeout;
        }

        public void Acquire([NotNull] string resource, TimeSpan timeout)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
            if (resource.Length == 0)
                throw new ArgumentException(null, nameof(resource));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, null);

            var deadline = DateTime.UtcNow + timeout;

            while (true)
            {
                if (TryAcquireLock(resource))
                    return;

                switch (TryReacquireLock(resource))
                {
                    case true:
                        return;
                    case false:
                        continue;
                }

                var remaining = deadline - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                    break;
                else if (remaining < s_maxSleepDuration)
                    Thread.Sleep(remaining);
                else
                    Thread.Sleep(s_maxSleepDuration);
            }

            throw new DistributedLockTimeoutException(resource);
        }

        public void Release([NotNull] string resource)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));

            _options.UseContext(context =>
            {
                context.Locks.Attach(new HangfireLock { Id = resource }).State = EntityState.Deleted;
                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else already has deleted this record. Database wins.
                }
            });
        }

        private bool TryAcquireLock(string resource)
        {
            return _options.UseContext(context =>
            {
                context.Locks.Add(new HangfireLock
                {
                    Id = resource,
                    AcquiredAt = DateTime.UtcNow,
                });

                try
                {
                    context.SaveChanges();
                    return true; // Lock taken
                }
                catch (DbUpdateException)
                {
                    return false; // Lock already exists
                }
            });
        }

        private bool? TryReacquireLock(string resource)
        {
            return _options.UseContext<bool?>(context =>
            {
                var distributedLock = context.Set<HangfireLock>().
                SingleOrDefault(x => x.Id == resource);

                // If the lock has been removed we should try to insert again
                if (distributedLock == null)
                    return false;

                var expireAt = distributedLock.AcquiredAt + _timeout;

                // If the lock has been expired, we should update its creation timestamp
                if (expireAt < DateTime.UtcNow)
                {
                    distributedLock.AcquiredAt = DateTime.UtcNow;

                    try
                    {
                        context.SaveChanges();
                        return true; // Lock taken
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        return false; // Already removed, we should try to insert again
                    }

                }

                return default;
            });
        }
    }
}
