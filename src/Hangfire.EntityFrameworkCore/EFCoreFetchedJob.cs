using System;
using System.Globalization;
using Hangfire.Annotations;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreFetchedJob : IFetchedJob
    {
        private readonly EFCoreStorage _storage;
        private readonly HangfireQueuedJob _queuedJob;
        private bool _disposed = false;
        private bool _completed = false;

        public long Id => _queuedJob.Id;
        public long JobId => _queuedJob.JobId;
        public string Queue => _queuedJob.Queue;

        string IFetchedJob.JobId => _queuedJob.JobId.ToString(CultureInfo.InvariantCulture);

        public EFCoreFetchedJob(
            [NotNull] EFCoreStorage storage,
            [NotNull] HangfireQueuedJob queuedJob)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _queuedJob = queuedJob ?? throw new ArgumentNullException(nameof(queuedJob));
        }


        public void RemoveFromQueue()
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

        public void Requeue()
        {
            _storage.UseContext(context =>
            {
                context.Attach(_queuedJob);
                _queuedJob.FetchedAt = null;
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
                    Requeue();
                _disposed = true;
            }
        }
    }
}
