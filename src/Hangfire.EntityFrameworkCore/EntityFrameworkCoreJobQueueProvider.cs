using System;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EntityFrameworkCoreJobQueueProvider : IPersistentJobQueueProvider
    {
        private readonly DbContextOptions<HangfireContext> _options;

        public EntityFrameworkCoreJobQueueProvider(DbContextOptions<HangfireContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IPersistentJobQueue GetJobQueue()
        {
            return new EntityFrameworkCoreJobQueue(_options);
        }

        public IPersistentJobQueueMonitoringApi GetMonitoringApi()
        {
            return new EntityFrameworkCoreJobQueueMonitoringApi(_options);
        }
    }
}
