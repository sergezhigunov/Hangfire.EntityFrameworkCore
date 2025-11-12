using System.Diagnostics.CodeAnalysis;
using Hangfire.EntityFrameworkCore.Properties;

namespace Hangfire.EntityFrameworkCore;

using GetLockFunc = Func<DbContext, string, HangfireLock>;
using NotNullAttribute = Annotations.NotNullAttribute;

internal class EFCoreLockProvider : ILockProvider
{
    private static readonly TimeSpan _maxSleepDuration = new(0, 0, 1);
    private readonly EFCoreStorage _storage;

    private static GetLockFunc GetLockFunc { get; } = EF.CompileQuery(
        (DbContext context, string id) =>
            context.Set<HangfireLock>().
            SingleOrDefault(x => x.Id == id));

    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreLockProvider(
        [NotNull] EFCoreStorage storage)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(storage);
#else
        if (storage is null) throw new ArgumentNullException(nameof(storage));
#endif
        _storage = storage;
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public void Acquire([NotNull] string resource, TimeSpan timeout)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrEmpty(resource);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
#else
        if (resource is null) throw new ArgumentNullException(nameof(resource));
        if (resource.Length == 0)
            throw new ArgumentException(
                CoreStrings.ArgumentExceptionCollectionCannotBeEmpty,
                nameof(resource));
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                CoreStrings.ArgumentOutOfRangeExceptionNeedNonNegativeValue);
#endif
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
            else if (remaining < _maxSleepDuration)
                Thread.Sleep(remaining);
            else
                Thread.Sleep(_maxSleepDuration);
        }
        throw new DistributedLockTimeoutException(resource);
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public void Release([NotNull] string resource)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrEmpty(resource);
#else
        if (resource is null) throw new ArgumentNullException(nameof(resource));
        if (resource.Length == 0)
            throw new ArgumentException(
                CoreStrings.ArgumentExceptionCollectionCannotBeEmpty,
                nameof(resource));
#endif
        _storage.UseContext(context =>
        {
            context.Remove(new HangfireLock
            {
                Id = resource,
            });
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
        => _storage.UseContext(
            context =>
            {
                context.Add(new HangfireLock
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

    private bool? TryReacquireLock(string resource)
        => _storage.UseContext<bool?>(
            context =>
            {
                var @lock = GetLockFunc(context, resource);
                // If the lock has been removed we should try to insert again
                if (@lock is null)
                    return false;
                // If the lock has been expired, we should update its creation timestamp
                var now = DateTime.UtcNow;
                if (@lock.AcquiredAt + _storage.DistributedLockTimeout < now)
                {
                    @lock.AcquiredAt = now;
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
