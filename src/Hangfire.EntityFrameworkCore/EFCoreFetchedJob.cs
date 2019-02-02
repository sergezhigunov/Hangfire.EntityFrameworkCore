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
        private readonly HangfireJobQueue _item;
        private bool _disposed = false;
        private bool _completed = false;

        public long Id => _item.Id;
        public long JobId => _item.JobId;
        public string Queue => _item.Queue;

        string IFetchedJob.JobId => _item.JobId.ToString(CultureInfo.InvariantCulture);

        public EFCoreFetchedJob(
            [NotNull] EFCoreStorage storage,
            [NotNull] HangfireJobQueue item)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _item = item ?? throw new ArgumentNullException(nameof(item));
        }


        public void RemoveFromQueue()
        {
            _storage.UseContext(context =>
            {
                context.Remove(_item);
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
                context.Attach(_item);
                _item.FetchedAt = null;
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
