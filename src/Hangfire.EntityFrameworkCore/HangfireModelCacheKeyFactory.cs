using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context) => new HangfireModelCacheKey(context);
    }
}
