﻿using System;
using System.Collections.Generic;
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
        [CLSCompliant(false)]
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
        [CLSCompliant(false)]
        public static IGlobalConfiguration<EFCoreStorage> UseEFCoreStorage(
            [NotNull] this IGlobalConfiguration configuration,
            [NotNull] Action<DbContextOptionsBuilder> optionsAction,
            [NotNull] EFCoreStorageOptions options)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            return configuration.UseStorage(new EFCoreStorage(optionsAction, options));
        }

        /// <summary>
        /// Creates and registers the <see cref="EFCoreStorage"/> in the global configuration with
        /// the specific options.
        /// </summary>
        /// <param name="configuration">
        /// The <see cref="IGlobalConfiguration"/> to add storage to.
        /// </param>
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
        /// <paramref name="configuration"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="contextBuilder"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        [CLSCompliant(false)]
        public static IGlobalConfiguration<EFCoreStorage> UseEFCoreStorage(
            [NotNull] this IGlobalConfiguration configuration,
            [NotNull] Func<DbContext> contextBuilder,
            [NotNull] EFCoreStorageOptions options)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            return configuration.UseStorage(new EFCoreStorage(contextBuilder, options));
        }

        /// <summary>
        /// Adds a database creator to the job storage.
        /// </summary>
        /// <param name="configuration">
        /// The storage configuration to add the database creator to.
        /// </param>
        /// <returns>
        /// A job storage configuration.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="configuration"/> is <see langword="null"/>.
        /// </exception>
        public static IGlobalConfiguration<EFCoreStorage> UseDatabaseCreator(
            [NotNull] this IGlobalConfiguration<EFCoreStorage> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Entry.RegisterDatabaseInitializer(
                context => context.Database.EnsureCreated());
            return configuration;
        }

        /// <summary>
        /// Adds a job queue provider of the specified queues into the storage.
        /// </summary>
        /// <param name="configuration">
        /// The storage configuration to add the job queue provider to.
        /// </param>
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
        /// <paramref name="configuration"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="provider"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="queues"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="queues"/> is empty.
        /// </exception>
        public static IGlobalConfiguration<EFCoreStorage> UseQueueProvider(
            [NotNull] this IGlobalConfiguration<EFCoreStorage> configuration,
            [NotNull] IPersistentJobQueueProvider provider,
            [NotNull] IList<string> queues)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Entry.RegisterProvider(provider, queues);
            return configuration;
        }
    }
}
