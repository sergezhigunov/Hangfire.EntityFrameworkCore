using System.Diagnostics.CodeAnalysis;
using NotNullAttribute = Hangfire.Annotations.NotNullAttribute;

namespace Hangfire.EntityFrameworkCore;

internal sealed class EFCoreLock : IDisposable
{
    private readonly ILockProvider _provider;
    private readonly string _resource;
    private bool _disposed;

    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreLock(
        [NotNull] ILockProvider provider,
        [NotNull] string resource,
        TimeSpan timeout)
    {
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));

        _provider = provider;
        _provider.Acquire(resource, timeout);
        _resource = resource;
    }

    void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                _provider.Release(_resource);

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
