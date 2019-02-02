using System;
using Hangfire.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// Extension methods for setting up Entity Framework Core job storage in an
    /// <see cref="IGlobalConfiguration"/>.
    /// </summary>
    public static class EFCoreStorageExtensions
    {
        /// <summary>
        /// Creates and registers the <see cref="EFCoreStorage"/> in the global configuration.
        /// </summary>
        /// <param name="configuration">
        /// The <see cref="IGlobalConfiguration"/> to add storage to.
        /// </param>
        /// <param name="optionsAction">
        /// An action to configure the <see cref="DbContextOptions"/> for the inner context.
        /// </param>
        /// <returns>
        /// Global configuration.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="configuration"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="optionsAction"/> is <see langword="null"/>.
        /// </exception>
        public static IGlobalConfiguration<EFCoreStorage> UseEFCoreStorage(
            [NotNull] this IGlobalConfiguration configuration,
            [NotNull] Action<DbContextOptionsBuilder> optionsAction)
        {
            return configuration.UseEFCoreStorage(optionsAction, new EFCoreStorageOptions());
        }

        /// <summary>
        /// Creates and registers the <see cref="EFCoreStorage"/> in the global configuration with
        /// the specific options.
        /// </summary>
        /// <param name="configuration">
        /// The <see cref="IGlobalConfiguration"/> to add storage to.
        /// </param>
        /// <param name="optionsAction">
        /// An action to configure the <see cref="DbContextOptions"/> for the inner context.
        /// </param>
        /// <param name="options">
        /// A specific storage options.
        /// </param>
        /// <returns>
        /// Global configuration.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="configuration"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="optionsAction"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        public static IGlobalConfiguration<EFCoreStorage> UseEFCoreStorage(
            [NotNull] this IGlobalConfiguration configuration,
            [NotNull] Action<DbContextOptionsBuilder> optionsAction,
            [NotNull] EFCoreStorageOptions options)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            return configuration.UseStorage(new EFCoreStorage(optionsAction, options));
        }
    }
}
