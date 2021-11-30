using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireModelCacheKeyFactory : IModelCacheKeyFactory
    {
#if !NET6_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
#endif
        public object Create(DbContext context, bool designTime)
            => context is HangfireContext hangfireContext
                ? (context.GetType(), hangfireContext.Schema, designTime)
                : (object)context.GetType();

        public object Create(DbContext context)
            => Create(context, false);
    }
}
