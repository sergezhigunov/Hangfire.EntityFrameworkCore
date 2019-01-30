using System;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class EFCoreJobStorage : JobStorage
    {
        private readonly DbContextOptions<HangfireContext> _options;

        public EFCoreJobStorage(
            DbContextOptions<HangfireContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override IStorageConnection GetConnection()
        {
            return new EFCoreJobStorageConnection(_options);
        }

        public override IMonitoringApi GetMonitoringApi()
        {
            return new EFCoreJobStorageMonitoringApi(_options);
        }
    }
}
