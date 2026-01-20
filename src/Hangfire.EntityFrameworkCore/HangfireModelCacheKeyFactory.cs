using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireModelCacheKeyFactory : IModelCacheKeyFactory
{
#if NET8_0_OR_GREATER
    public object Create(DbContext context, bool designTime)
        => context is HangfireContext hangfireContext
            ? (context.GetType(), hangfireContext.Schema, designTime)
            : (object)context.GetType();
#else
    public object Create(DbContext context)
        => context is HangfireContext hangfireContext
            ? (context.GetType(), hangfireContext.Schema, false)
            : (object)context.GetType();
#endif
}
