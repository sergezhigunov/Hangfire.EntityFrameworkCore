using System;
using System.Collections.Generic;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// Represents an Entity Framework Core based Hangfire Job Storage.
    /// </summary>
    public class EFCoreStorage : JobStorage
    {
        private readonly object _lock = new object();
        private readonly DbContextOptions _contextOptions;
        private readonly EFCoreStorageOptions _options;
        private Action<HangfireContext> _databaseInitializer;
        private bool _databaseInitialized;

        internal EFCoreJobQueueProvider DefaultQueueProvider { get; }

        internal IDictionary<string, IPersistentJobQueueProvider> QueueProviders { get; } =
            new Dictionary<string, IPersistentJobQueueProvider>(StringComparer.OrdinalIgnoreCase);

        internal TimeSpan DistributedLockTimeout => _options.DistributedLockTimeout;

        internal TimeSpan QueuePollInterval => _options.QueuePollInterval;

        internal TimeSpan JobExpirationCheckInterval => _options.JobExpirationCheckInterval;

        internal TimeSpan CountersAggregationInterval => _options.CountersAggregationInterval;

        internal TimeSpan SlidingInvisibilityTimeout => _options.SlidingInvisibilityTimeout;

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
        [CLSCompliant(false)]
        public EFCoreStorage(
            Action<DbContextOptionsBuilder> optionsAction,
            EFCoreStorageOptions options)
        {
            if (optionsAction is null)
                throw new ArgumentNullException(nameof(optionsAction));
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _options = options;
            var contextOptionsBuilder = new DbContextOptionsBuilder<HangfireContext>();
            optionsAction.Invoke(contextOptionsBuilder);
            _contextOptions = contextOptionsBuilder.Options;
            DefaultQueueProvider = new EFCoreJobQueueProvider(this);
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

        /// <summary>
        /// Returns of server component collection.
        /// </summary>
        /// <returns>
        /// Collection of server components <see cref="IServerComponent"/>.
        /// </returns>
#pragma warning disable CS0618
        public override IEnumerable<IServerComponent> GetComponents()
#pragma warning restore CS0618
        {
            foreach (var item in base.GetComponents())
                yield return item;
            yield return new ExpirationManager(this);
            yield return new CountersAggregator(this);
        }

        internal IPersistentJobQueueProvider GetQueueProvider(string queue)
        {
            if (queue is null)
                throw new ArgumentNullException(nameof(queue));

            return QueueProviders.GetValue(queue) ?? DefaultQueueProvider;
        }

        internal void RegisterDatabaseInitializer(Action<HangfireContext> databaseInitializer)
            => _databaseInitializer = databaseInitializer;

        internal void RegisterProvider(IPersistentJobQueueProvider provider, IList<string> queues)
        {
            if (provider is null)
                throw new ArgumentNullException(nameof(provider));
            if (queues is null)
                throw new ArgumentNullException(nameof(queues));
            if (queues.Count == 0)
                throw new ArgumentException(CoreStrings.ArgumentExceptionCollectionCannotBeEmpty,
                    nameof(queues));

            var providers = QueueProviders;

            foreach (var queue in queues)
                providers[queue] = provider;
        }

        internal void UseContext(Action<HangfireContext> action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            using var context = CreateContext();
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
            if (func is null)
                throw new ArgumentNullException(nameof(func));

            using var context = CreateContext();
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
            var context = new HangfireContext(_contextOptions, _options.Schema);
            if (!_databaseInitialized)
                lock (_lock)
                    if (!_databaseInitialized)
                    {
                        _databaseInitializer?.Invoke(context);
                        _databaseInitialized = true;
                    }

            return context;
        }
    }
}
