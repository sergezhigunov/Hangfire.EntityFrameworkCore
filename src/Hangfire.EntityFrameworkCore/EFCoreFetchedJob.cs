using System;
using System.Globalization;
using System.Threading;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Logging;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using NotNullAttribute = Hangfire.Annotations.NotNullAttribute;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreFetchedJob : IFetchedJob
    {
        private readonly ILog _logger = LogProvider.GetLogger(typeof(EFCoreFetchedJob));
        private readonly object _lock = new();
        private readonly EFCoreStorage _storage;
        private readonly Timer _timer;
        private readonly HangfireQueuedJob _queuedJob;
        private bool _disposed;
        private bool _completed;

        public long Id => _queuedJob.Id;
        public long JobId => _queuedJob.JobId;
        public string Queue => _queuedJob.Queue;

        string IFetchedJob.JobId => _queuedJob.JobId.ToString(CultureInfo.InvariantCulture);

        public EFCoreFetchedJob(
            [NotNull] EFCoreStorage storage,
            [NotNull] HangfireQueuedJob queuedJob)
        {
            if (storage is null)
                throw new ArgumentNullException(nameof(storage));
            if (queuedJob is null)
                throw new ArgumentNullException(nameof(queuedJob));

            _storage = storage;
            _queuedJob = queuedJob;
            var keepAliveInterval = new TimeSpan(storage.SlidingInvisibilityTimeout.Ticks / 5);
            _timer = new Timer(ExecuteKeepAliveQuery, null, keepAliveInterval, keepAliveInterval);
        }

        public void RemoveFromQueue()
        {
            lock (_lock)
            {
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
                _completed = true;
            }
        }

        public void Requeue()
        {
            lock (_lock)
            {
                SetFetchedAt(null);
                _completed = true;
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

        private void ExecuteKeepAliveQuery(object state)
        {
            lock (_lock)
            {
                if (_completed)
                    return;
                try
                {
                    SetFetchedAt(DateTime.UtcNow);
                }
                catch (Exception exception)
                {
                    _logger.Log(LogLevel.Debug, () =>
                        CoreStrings.EFCoreFetchedJobExecuteKeepAliveQueryFailed(Id),
                        exception);
                    return;
                }
                _logger.Trace(CoreStrings.EFCoreFetchedJobExecuteKeepAliveQueryCompleted(Id));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && !_completed)
                {
                    _timer.Dispose();
                    Requeue();
                }
                _disposed = true;
            }
        }
    }
}
