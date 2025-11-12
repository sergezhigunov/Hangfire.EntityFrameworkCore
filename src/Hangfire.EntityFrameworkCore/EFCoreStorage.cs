using System.Diagnostics.CodeAnalysis;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents an Entity Framework Core based Hangfire Job Storage.
/// </summary>
public class EFCoreStorage : JobStorage
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly DbContextOptions _contextOptions;
    private readonly EFCoreStorageOptions _options;
    private readonly Func<DbContext> _dbContextBuilder;
    private Action<HangfireContext> _databaseInitializer;
    private bool _databaseInitialized;

    internal EFCoreJobQueueProvider DefaultQueueProvider { get; }

    internal IDictionary<string, IPersistentJobQueueProvider> QueueProviders { get; } =
        new Dictionary<string, IPersistentJobQueueProvider>(StringComparer.OrdinalIgnoreCase);

    internal EFCoreHeartbeatProcess HeartbeatProcess { get; }

    internal TimeSpan DistributedLockTimeout => _options.DistributedLockTimeout;

    internal TimeSpan QueuePollInterval => _options.QueuePollInterval;

    internal TimeSpan JobExpirationCheckInterval => _options.JobExpirationCheckInterval;

    internal TimeSpan CountersAggregationInterval => _options.CountersAggregationInterval;

    internal TimeSpan SlidingInvisibilityTimeout => _options.SlidingInvisibilityTimeout;

    internal bool UseSlidingInvisibilityTimeout => _options.UseSlidingInvisibilityTimeout;

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
    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreStorage(
        Action<DbContextOptionsBuilder> optionsAction,
        EFCoreStorageOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(optionsAction);
        ArgumentNullException.ThrowIfNull(options);
#else
        if (optionsAction is null) throw new ArgumentNullException(nameof(optionsAction));
        if (options is null) throw new ArgumentNullException(nameof(options));
#endif
        _options = options;
        var contextOptionsBuilder = new DbContextOptionsBuilder<HangfireContext>();
        optionsAction.Invoke(contextOptionsBuilder);
        _contextOptions = contextOptionsBuilder.Options;
        DefaultQueueProvider = new EFCoreJobQueueProvider(this);
        if (UseSlidingInvisibilityTimeout)
        {
            HeartbeatProcess = new EFCoreHeartbeatProcess();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EFCoreStorage"/> class.
    /// </summary>
    /// <param name="dbContextBuilder">
    /// A factory func that returns a new DbContext for storing jobs.
    /// </param>
    /// <param name="options">
    /// Any specific storage options.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dbContextBuilder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    [CLSCompliant(false)]
    public EFCoreStorage(
        Func<DbContext> dbContextBuilder,
        EFCoreStorageOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dbContextBuilder);
        ArgumentNullException.ThrowIfNull(options);
#else
        if (dbContextBuilder is null) throw new ArgumentNullException(nameof(dbContextBuilder));
        if (options is null) throw new ArgumentNullException(nameof(options));
#endif
        _dbContextBuilder = dbContextBuilder;
        _options = options;
        DefaultQueueProvider = new EFCoreJobQueueProvider(this);
        if (UseSlidingInvisibilityTimeout)
            HeartbeatProcess = new EFCoreHeartbeatProcess();
    }

    /// <summary>
    /// Creates a new job storage connection.
    /// </summary>
    /// <returns>
    /// A new job storage connection.
    /// </returns>
    public override IStorageConnection GetConnection() => new EFCoreStorageConnection(this);

    /// <summary>
    /// Creates a new job storage monitoring API.
    /// </summary>
    /// <returns>
    /// A new job storage monitoring API.
    /// </returns>
    public override IMonitoringApi GetMonitoringApi() => new EFCoreStorageMonitoringApi(this);

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
        if (UseSlidingInvisibilityTimeout)
        {
            // This is only used to update the sliding invisibility timeouts, so if not enabled then do not use it
            yield return HeartbeatProcess;
        }
    }

    [SuppressMessage("Maintainability", "CA1510")]
    internal IPersistentJobQueueProvider GetQueueProvider(string queue)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(queue);
#else
        if (queue is null) throw new ArgumentNullException(nameof(queue));
#endif
        return QueueProviders.GetValue(queue) ?? DefaultQueueProvider;
    }

    internal void RegisterDatabaseInitializer(Action<HangfireContext> databaseInitializer)
        => _databaseInitializer = databaseInitializer;

    [SuppressMessage("Maintainability", "CA1510")]
    internal void RegisterProvider(IPersistentJobQueueProvider provider, IList<string> queues)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(queues);
#else
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        if (queues is null) throw new ArgumentNullException(nameof(queues));
#endif
        if (queues.Count == 0)
            throw new ArgumentException(CoreStrings.ArgumentExceptionCollectionCannotBeEmpty,
                nameof(queues));
        var providers = QueueProviders;
        foreach (var queue in queues)
            providers[queue] = provider;
    }

    [SuppressMessage("Maintainability", "CA1510")]
    internal void UseContext(Action<DbContext> action)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(action);
#else
        if (action is null) throw new ArgumentNullException(nameof(action));
#endif
        using var context = CreateContext();
        action(context);
    }

    internal void UseContextSavingChanges(Action<DbContext> action)
    {
        UseContext(context =>
        {
            action(context);
            context.SaveChanges();
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    internal T UseContext<T>(Func<DbContext, T> func)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(func);
#else
        if (func is null) throw new ArgumentNullException(nameof(func));
#endif
        using var context = CreateContext();
        return func(context);
    }

    internal T UseContextSavingChanges<T>(Func<DbContext, T> func)
    {
        return UseContext(context =>
        {
            var result = func(context);
            context.SaveChanges();
            return result;
        });
    }

    internal DbContext CreateContext()
    {
        if (_dbContextBuilder != null)
            return _dbContextBuilder();
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
