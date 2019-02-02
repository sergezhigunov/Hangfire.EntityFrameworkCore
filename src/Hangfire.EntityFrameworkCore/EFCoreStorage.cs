using System;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// Represents an Entity Framework Core based Hangfire Job Storage.
    /// </summary>
    public class EFCoreStorage : JobStorage
    {
        private readonly DbContextOptions _contextOptions;
        private readonly EFCoreStorageOptions _options;

        internal TimeSpan DistributedLockTimeout => _options.DistributedLockTimeout;

        internal TimeSpan QueuePollInterval => _options.QueuePollInterval;

        /// <summary>
        /// Initializes a new instance of the <see cref="EFCoreStorage"/> class.
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
        public EFCoreStorage(
            Action<DbContextOptionsBuilder> optionsAction,
            EFCoreStorageOptions options)
        {
            if (optionsAction == null)
                throw new ArgumentNullException(nameof(optionsAction));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            var contextOptionsBuilder = new DbContextOptionsBuilder<HangfireContext>();
            optionsAction.Invoke(contextOptionsBuilder);
            _contextOptions = contextOptionsBuilder.Options;
        }

        /// <summary>
        /// Creates a new job storage connection.
        /// </summary>
        /// <returns>
        /// A new job storage connection.
        /// </returns>
        public override IStorageConnection GetConnection()
        {
            return new EFCoreStorageConnection(this);
        }

        /// <summary>
        /// Creates a new job storage monitoring API.
        /// </summary>
        /// <returns>
        /// A new job storage monitoring API.
        /// </returns>
        public override IMonitoringApi GetMonitoringApi()
        {
            return new EFCoreStorageMonitoringApi(this);
        }

        internal void UseContext(Action<HangfireContext> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            using (var context = CreateContext())
                action(context);
        }

        internal void UseContextSavingChanges(Action<HangfireContext> action)
        {
            UseContext(context =>
            {
                action(context);
                context.SaveChanges();
            });
        }

        internal T UseContext<T>(Func<HangfireContext, T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            using (var context = CreateContext())
                return func(context);
        }

        internal T UseContextSavingChanges<T>(Func<HangfireContext, T> func)
        {
            return UseContext(context =>
            {
                var result = func(context);
                context.SaveChanges();
                return result;
            });
        }

        internal HangfireContext CreateContext()
        {
            return new HangfireContext(_contextOptions);
        }
    }
}
