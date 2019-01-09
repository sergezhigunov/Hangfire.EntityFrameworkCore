using System;
using Hangfire.Annotations;
using Hangfire.Storage;

namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// Provides a mechanism that allows to acquire and release exclusive distributed locks.
    /// </summary>
    public interface IDistributedLockProvider
    {
        /// <summary>
        /// Acquires an exclusive distributed lock on the specified resource.
        /// </summary>
        /// <param name="resource">
        /// The resource on which to acquire the lock.
        /// </param>
        /// <param name="timeout">
        /// A <see cref="TimeSpan"/> representing the amount of time to wait for the lock.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="resource"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="DistributedLockTimeoutException">
        /// The timeout elapsed prior to obtaining a distributed lock on the resource.
        /// </exception>
        void Acquire([NotNull] string resource, TimeSpan timeout);

        /// <summary>
        /// Acquires an exclusive distributed lock on the specified resource.
        /// </summary>
        /// <param name="resource">
        /// The resource on which to release the lock.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="resource"/> is <see langword="null"/>.
        /// </exception>
        void Release([NotNull] string resource);
    }
}
