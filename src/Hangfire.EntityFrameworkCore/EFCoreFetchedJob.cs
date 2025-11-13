using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Hangfire.EntityFrameworkCore.Properties;
using NotNullAttribute = Hangfire.Annotations.NotNullAttribute;

namespace Hangfire.EntityFrameworkCore;

internal sealed class EFCoreFetchedJob : IFetchedJob
{
    private readonly ILog _logger = LogProvider.GetLogger(typeof(EFCoreFetchedJob));
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly EFCoreStorage _storage;
    private readonly HangfireQueuedJob _queuedJob;
    private bool _disposed;
    private bool _removedFromQueue;
    private bool _requeued;
    private readonly long _lastHeartbeat;
    private readonly TimeSpan _interval;

    public long Id => _queuedJob.Id;
    public long JobId => _queuedJob.JobId;
    public string Queue => _queuedJob.Queue;
    internal DateTime? FetchedAt => _queuedJob.FetchedAt;

    string IFetchedJob.JobId => _queuedJob.JobId.ToString(CultureInfo.InvariantCulture);

    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreFetchedJob(
        [NotNull] EFCoreStorage storage,
        [NotNull] HangfireQueuedJob queuedJob)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(queuedJob);
#else
        if (storage is null) throw new ArgumentNullException(nameof(storage));
        if (queuedJob is null) throw new ArgumentNullException(nameof(queuedJob));
#endif
        _storage = storage;
        _queuedJob = queuedJob;

        if (storage.UseSlidingInvisibilityTimeout)
        {
            _lastHeartbeat = TimestampHelper.GetTimestamp();
            _interval = TimeSpan.FromSeconds(storage.SlidingInvisibilityTimeout.TotalSeconds / 5);
            storage.HeartbeatProcess.Track(this);
        }
    }

    public void RemoveFromQueue()
    {
        lock (_lock)
        {
            if (!FetchedAt.HasValue)
                return;
            _storage.UseContext(context =>
            {
                context.Remove(_queuedJob);
                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else already has removed item, database wins
                }
            });
            _removedFromQueue = true;
        }
    }

    public void Requeue()
    {
        lock (_lock)
        {
            if (!FetchedAt.HasValue)
                return;
            SetFetchedAt(null);
            _requeued = true;
        }
    }

    private void SetFetchedAt(DateTime? value)
    {
        _storage.UseContext(context =>
        {
            context.Attach(_queuedJob);
            _queuedJob.FetchedAt = value;
            try
            {
                context.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else already has removed item, database wins
            }
        });
    }

    internal void DisposeTimer()
    {
        if (_storage.UseSlidingInvisibilityTimeout)
            _storage.HeartbeatProcess.Untrack(this);
    }

    [SuppressMessage("Design", "CA1031")]
    internal void ExecuteKeepAliveQueryIfRequired()
    {
        if (TimestampHelper.Elapsed(_lastHeartbeat) < _interval)
            return;
        lock (_lock)
        {
            if (!FetchedAt.HasValue)
                return;
            if (_requeued || _removedFromQueue)
                return;
            try
            {
                SetFetchedAt(DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                _logger.Log(LogLevel.Debug, () =>
                    string.Format(null, CoreStrings.EFCoreFetchedJobExecuteKeepAliveQueryFailed, Id),
                    exception);
                return;
            }
            _logger.Trace(string.Format(null, CoreStrings.EFCoreFetchedJobExecuteKeepAliveQueryCompleted, Id));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeTimer();
        lock (_lock)
            if (!_removedFromQueue && !_requeued)
                Requeue();
    }
}
