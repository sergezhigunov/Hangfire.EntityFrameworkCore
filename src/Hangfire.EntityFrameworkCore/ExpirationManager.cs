using System;
using System.Linq;
using System.Threading;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore;

#pragma warning disable 618
internal class ExpirationManager : IServerComponent
#pragma warning restore 618
{
    private const int BatchSize = 1000;
    private const string LockKey = "locks:expirationmanager";
    private readonly ILog _logger = LogProvider.For<ExpirationManager>();
    private readonly EFCoreStorage _storage;

    public ExpirationManager(EFCoreStorage storage)
    {
        if (storage is null)
            throw new ArgumentNullException(nameof(storage));

        _storage = storage;
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
        _logger.Debug(CoreStrings.ExpirationManagerRemoveExpiredStarting(type.Name));

        UseLock(() =>
        {
            while (0 != _storage.UseContext(context =>
                 {
                var expiredEntities = context
                    .Set<T>()
                    .Where(x => x.ExpireAt < DateTime.UtcNow)
                    .Take(BatchSize)
                    .ToList();
                if (expiredEntities.Count == 0)
                    return 0;
                context.RemoveRange(expiredEntities);
                try
                {
                    return context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else already has removed item, database wins. Just try again.
                    return -1;
                }
            }));
        });

        _logger.Trace(CoreStrings.ExpirationManagerRemoveExpiredCompleted(type.Name));
    }

    private void UseLock(Action action)
    {
        var lockTimeout = _storage.DistributedLockTimeout;
        using var connection = _storage.GetConnection();
        try
        {
            using (connection.AcquireDistributedLock(LockKey, lockTimeout))
                action.Invoke();
        }
        catch (DistributedLockTimeoutException exception)
        when (exception.Resource == LockKey)
        {
            _logger.Log(LogLevel.Debug, () =>
                 CoreStrings.ExpirationManagerUseLockFailed(
                    LockKey,
                    lockTimeout.TotalSeconds,
                    _storage.JobExpirationCheckInterval.TotalSeconds),
                exception);
        }
    }
}
