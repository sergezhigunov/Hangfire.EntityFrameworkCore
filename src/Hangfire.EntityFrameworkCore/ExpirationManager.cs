using System;
using System.Linq;
using System.Threading;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;

namespace Hangfire.EntityFrameworkCore
{
#pragma warning disable 618
    internal class ExpirationManager : IServerComponent
#pragma warning restore 618
    {
        private const string LockKey = "locks:expirationmanager";
        private readonly ILog _logger = LogProvider.For<ExpirationManager>();
        private readonly EFCoreStorage _storage;

        public ExpirationManager(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            RemoveExpired<HangfireCounter>();
            RemoveExpired<HangfireHash>();
            RemoveExpired<HangfireList>();
            RemoveExpired<HangfireSet>();
            RemoveExpired<HangfireJob>();
            cancellationToken.WaitHandle.WaitOne(_storage.JobExpirationCheckInterval);
        }

        private void RemoveExpired<T>()
            where T : class, IExpirable
        {
            var type = typeof(T);
            _logger.Debug(
               $"Removing outdated records from the '{type.Name}' table...");

            UseLock(() => _storage.UseContextSavingChanges(context =>
            {
                var set = context.Set<T>();
                set.RemoveRange(set.Where(x => x.ExpireAt < DateTime.UtcNow));
            }));

            _logger.Trace($"Outdated records removed from the '{type.Name}' table.");
        }

        private void UseLock(Action action)
        {
            var lockTimeout = _storage.DistributedLockTimeout;
            using (var connection = _storage.GetConnection())
            {
                try
                {
                    using (var @lock = connection.AcquireDistributedLock(LockKey, lockTimeout))
                        action.Invoke();
                }
                catch (DistributedLockTimeoutException exception)
                when (exception.Resource == LockKey)
                {
                    _logger.Log(LogLevel.Debug, () =>
                        $@"An exception was thrown during acquiring distributed lock on the {
                            LockKey
                        } resource within {
                            lockTimeout.TotalSeconds
                        } seconds. Outdated records were not removed.
It will be retried in {_storage.JobExpirationCheckInterval.TotalSeconds} seconds.",
                        exception);
                }
            }
        }
    }
}
