using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore;

#pragma warning disable 618
internal class ExpirationManager : IServerComponent
#pragma warning restore 618
{
    private const int BatchSize = 1000;
    private const string LockKey = "locks:expirationmanager";
    private readonly ILog _logger = LogProvider.For<ExpirationManager>();
    private readonly EFCoreStorage _storage;

    [SuppressMessage("Maintainability", "CA1510")]
    public ExpirationManager(EFCoreStorage storage)
    {
        if (storage is null)
            throw new ArgumentNullException(nameof(storage));

        _storage = storage;
    }

    public void Execute(CancellationToken cancellationToken)
    {
        RemoveExpired(
            (HangfireCounter x) => x.Id,
            x => new HangfireCounter { Id = x });
        RemoveExpired(
            (HangfireHash x) => new { x.Key, x.Field },
            x => new HangfireHash { Key = x.Key, Field = x.Field });
        RemoveExpired(
            (HangfireList x) => new { x.Key, x.Position },
            x => new HangfireList { Key = x.Key, Position = x.Position });
        RemoveExpired(
            (HangfireSet x) => new { x.Key, x.Value },
            x => new HangfireSet { Key = x.Key, Value = x.Value });
        RemoveExpiredJobs();
        cancellationToken.WaitHandle.WaitOne(_storage.JobExpirationCheckInterval);
    }

    private void RemoveExpiredJobs()
    {
        var type = typeof(HangfireJob);
        _logger.Debug(CoreStrings.ExpirationManagerRemoveExpiredStarting(type.Name));

        UseLock(() =>
        {
            while (0 != _storage.UseContext(context =>
            {
                var expiredEntityIds = GetExpiredIds(context, (HangfireJob x) => x.Id);
                if (expiredEntityIds.Count == 0)
                    return 0;
                var entries = expiredEntityIds
                    .Select(x => context.Attach(new HangfireJob { Id = x }))
                    .ToList();

                // Trying to set StateId = null for all fetched jobs first
                foreach (var entry in entries)
                    entry.Property(x => x.StateId).IsModified = true;

                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else already has removed item, database wins. Just try again.
                    return -1;
                }

                // After setting StateId = null remove all fetched jobs
                foreach (var entry in entries)
                    entry.State = EntityState.Deleted;
                int affected;
                try
                {
                    affected = context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else already has removed item, database wins. Just try again.
                    return -1;
                }
                return affected;
            }));
        });

        _logger.Trace(CoreStrings.ExpirationManagerRemoveExpiredCompleted(type.Name));
    }

    private void RemoveExpired<TEntity, TKey>(
        Expression<Func<TEntity, TKey>> keySelector,
        Func<TKey, TEntity> entityFactory)
        where TEntity : class, IExpirable
    {
        var type = typeof(TEntity);
        _logger.Debug(CoreStrings.ExpirationManagerRemoveExpiredStarting(type.Name));

        UseLock(() =>
        {
            while (0 != _storage.UseContext(context =>
            {
                var expiredEntityIds = GetExpiredIds(context, keySelector);
                if (expiredEntityIds.Count == 0)
                    return 0;

                var expiredEntities = expiredEntityIds
                    .Select(entityFactory)
                    .ToList();

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

    [SuppressMessage("Performance", "CA1859")]
    private static IReadOnlyCollection<TKey> GetExpiredIds<TEntity, TKey>(
        DbContext context,
        Expression<Func<TEntity, TKey>> keySelector)
        where TEntity : class, IExpirable
    {
        return context
            .Set<TEntity>()
            .Where(x => x.ExpireAt < DateTime.UtcNow)
            .Select(keySelector)
            .Take(BatchSize)
            .ToList();
    }
}
