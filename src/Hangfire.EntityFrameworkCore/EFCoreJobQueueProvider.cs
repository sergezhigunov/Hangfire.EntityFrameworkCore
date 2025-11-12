using System.Diagnostics.CodeAnalysis;

namespace Hangfire.EntityFrameworkCore;

internal sealed class EFCoreJobQueueProvider : IPersistentJobQueueProvider
{
    private readonly EFCoreStorage _storage;

    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreJobQueueProvider(EFCoreStorage storage)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(storage);
#else
        if (storage is null) throw new ArgumentNullException(nameof(storage));
#endif
        _storage = storage;
    }

    public IPersistentJobQueue GetJobQueue() => new EFCoreJobQueue(_storage);

    public IPersistentJobQueueMonitoringApi GetMonitoringApi() => new EFCoreJobQueueMonitoringApi(_storage);
}
