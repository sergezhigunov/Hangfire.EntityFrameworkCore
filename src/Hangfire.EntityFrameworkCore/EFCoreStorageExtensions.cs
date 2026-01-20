namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Extension methods for setting up Entity Framework Core job storage in an
/// <see cref="IGlobalConfiguration"/>.
/// </summary>
#pragma warning disable CA1708
public static class EFCoreStorageExtensions
#pragma warning restore CA1708
{
    /// <param name="configuration">
    /// The <see cref="IGlobalConfiguration"/> to add storage to.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
#pragma warning disable CA1034
    extension([NotNull] IGlobalConfiguration configuration)
#pragma warning restore CA1034
    {
        /// <summary>
        /// Creates and registers the <see cref="EFCoreStorage"/> in the global configuration.
        /// </summary>
        /// <param name="optionsAction">
        /// An action to configure the <see cref="DbContextOptions"/> for the inner context.
        /// </param>
        /// <returns>
        /// Global configuration.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="optionsAction"/> is <see langword="null"/>.
        /// </exception>
        [CLSCompliant(false)]
        public IGlobalConfiguration<EFCoreStorage> UseEFCoreStorage(
            [NotNull] Action<DbContextOptionsBuilder> optionsAction)
        {
            return configuration.UseEFCoreStorage(optionsAction, new EFCoreStorageOptions());
        }

        /// <summary>
        /// Creates and registers the <see cref="EFCoreStorage"/> in the global configuration with
        /// the specific options.
        /// </summary>
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
        /// <paramref name="optionsAction"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        [CLSCompliant(false)]
        public IGlobalConfiguration<EFCoreStorage> UseEFCoreStorage(
            [NotNull] Action<DbContextOptionsBuilder> optionsAction,
            [NotNull] EFCoreStorageOptions options)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(configuration);
#else
#pragma warning disable CA2208
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
#pragma warning restore CA2208
#endif
            return configuration.UseStorage(new EFCoreStorage(optionsAction, options));
        }

        /// <summary>
        /// Creates and registers the <see cref="EFCoreStorage"/> in the global configuration with
        /// the specific options.
        /// </summary>
        /// <param name="contextBuilder">
        /// Factory function for creating a new DbContext.
        /// </param>
        /// <param name="options">
        /// A specific storage options.
        /// </param>
        /// <returns>
        /// Global configuration.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="contextBuilder"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        [CLSCompliant(false)]
        public IGlobalConfiguration<EFCoreStorage> UseEFCoreStorage(
            [NotNull] Func<DbContext> contextBuilder,
            [NotNull] EFCoreStorageOptions options)
        {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(contextBuilder);
        ArgumentNullException.ThrowIfNull(options);
#else
#pragma warning disable CA2208
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
#pragma warning restore CA2208
            if (contextBuilder is null) throw new ArgumentNullException(nameof(contextBuilder));
            if (options is null) throw new ArgumentNullException(nameof(options));
#endif
            return configuration.UseStorage(new EFCoreStorage(contextBuilder, options));
        }
    }

    /// <param name="configuration">
    /// The storage configuration to use.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
#pragma warning disable CA1034
    extension([NotNull] IGlobalConfiguration<EFCoreStorage> configuration)
    {
        /// <summary>
        /// Adds a database creator to the job storage.
        /// </summary>
        /// <returns>
        /// A job storage configuration.
        /// </returns>
        public IGlobalConfiguration<EFCoreStorage> UseDatabaseCreator()
        {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(configuration);
#else
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
#endif
            configuration.Entry.RegisterDatabaseInitializer(
                context => context.Database.EnsureCreated());
            return configuration;
        }

        /// <summary>
        /// Adds a job queue provider of the specified queues into the storage.
        /// </summary>
        /// <param name="provider">
        /// A job queue provider to add.
        /// </param>
        /// <param name="queues">
        /// A list of provider queues.
        /// </param>
        /// <returns>
        /// A job storage configuration.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="provider"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="queues"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="queues"/> is empty.
        /// </exception>
        public IGlobalConfiguration<EFCoreStorage> UseQueueProvider(
            [NotNull] IPersistentJobQueueProvider provider,
            [NotNull] IList<string> queues)
        {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(configuration);
#else
#pragma warning disable CA2208
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
#pragma warning restore CA2208
#endif
            configuration.Entry.RegisterProvider(provider, queues);
            return configuration;
        }
    }
}
