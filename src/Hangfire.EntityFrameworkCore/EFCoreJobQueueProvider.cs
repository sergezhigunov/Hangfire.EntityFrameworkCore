using System.Diagnostics.CodeAnalysis;

namespace Hangfire.EntityFrameworkCore;

internal sealed class EFCoreJobQueueProvider : IPersistentJobQueueProvider
{
    private readonly EFCoreStorage _storage;

    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreJobQueueProvider(EFCoreStorage storage)
    {
        if (storage is null)
            throw new ArgumentNullException(nameof(storage));

        _storage = storage;
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
