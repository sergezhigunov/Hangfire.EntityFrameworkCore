using System;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class EntityFrameworkCoreJobStorage : JobStorage
    {
        private readonly DbContextOptions<HangfireContext> _options;

        public EntityFrameworkCoreJobStorage(
            DbContextOptions<HangfireContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override IStorageConnection GetConnection()
        {
            return new EntityFrameworkCoreJobStorageConnection(_options);
        }

        public override IMonitoringApi GetMonitoringApi()
        {
            return new EntityFrameworkCoreJobStorageMonitoringApi(_options);
        }
    }
}
