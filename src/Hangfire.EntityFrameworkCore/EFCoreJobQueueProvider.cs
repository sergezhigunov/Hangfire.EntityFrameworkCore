using System;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreJobQueueProvider : IPersistentJobQueueProvider
    {
        private readonly DbContextOptions _options;

        public EFCoreJobQueueProvider(DbContextOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IPersistentJobQueue GetJobQueue()
        {
            return new EFCoreJobQueue(_options);
        }

        public IPersistentJobQueueMonitoringApi GetMonitoringApi()
        {
            return new EFCoreJobQueueMonitoringApi(_options);
        }
    }
}
