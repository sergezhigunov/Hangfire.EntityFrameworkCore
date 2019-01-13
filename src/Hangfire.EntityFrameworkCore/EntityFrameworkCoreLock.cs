using System;
using Hangfire.Annotations;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EntityFrameworkCoreLock : IDisposable
    {
        private readonly IDistributedLockProvider _provider;
        private readonly string _resource;
        private bool _disposed = false;

        public EntityFrameworkCoreLock(
            [NotNull] IDistributedLockProvider provider,
            [NotNull] string resource,
            TimeSpan timeout)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
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
}
