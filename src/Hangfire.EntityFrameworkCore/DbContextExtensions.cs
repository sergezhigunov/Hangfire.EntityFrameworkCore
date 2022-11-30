using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Hangfire.EntityFrameworkCore;

internal static class DbContextExtensions
{
    public static ICollection<EntityEntry<T>> FindEntries<T>(this DbContext context, Func<T, bool> predicate)
        where T : class
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        return context.ChangeTracker
            .Entries<T>()
            .Where(x => predicate(x.Entity))
            .ToList();
    }

    public static EntityEntry<T> FindEntry<T>(this DbContext context, Func<T, bool> predicate)
        where T : class
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        return context.ChangeTracker
            .Entries<T>()
            .FirstOrDefault(x => predicate(x.Entity));
    }
}
