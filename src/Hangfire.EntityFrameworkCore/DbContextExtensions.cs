using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Hangfire.EntityFrameworkCore;

internal static class DbContextExtensions
{
    extension<T>(DbContext context)
         where T : class
    {
        public ICollection<EntityEntry<T>> FindEntries(Func<T, bool> predicate)
            => [.. context.FindEntriesCore(predicate)];

        public EntityEntry<T> FindEntry(Func<T, bool> predicate)
            => context.FindEntriesCore(predicate).FirstOrDefault();

        private IEnumerable<EntityEntry<T>> FindEntriesCore(Func<T, bool> predicate)
            => context.ChangeTracker.Entries<T>().Where(x => predicate(x.Entity));
    }
}
