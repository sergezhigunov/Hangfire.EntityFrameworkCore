using System;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreJobQueueProvider : IPersistentJobQueueProvider
    {
        private readonly EFCoreStorage _storage;

        public EFCoreJobQueueProvider(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IPersistentJobQueue GetJobQueue()
        {
            return new EFCoreJobQueue(_storage);
        }

        public IPersistentJobQueueMonitoringApi GetMonitoringApi()
        {
            return new EFCoreJobQueueMonitoringApi(_storage);
        }
    }
}
