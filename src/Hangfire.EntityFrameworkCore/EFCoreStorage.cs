using System;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class EFCoreStorage : JobStorage
    {
        private readonly DbContextOptions<HangfireContext> _options;

        public EFCoreStorage(
            DbContextOptions<HangfireContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override IStorageConnection GetConnection()
        {
            return new EFCoreStorageConnection(_options);
        }

        public override IMonitoringApi GetMonitoringApi()
        {
            return new EFCoreStorageMonitoringApi(_options);
        }
    }
}
