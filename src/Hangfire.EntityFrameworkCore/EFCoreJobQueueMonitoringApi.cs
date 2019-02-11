using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Hangfire.Annotations;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreJobQueueMonitoringApi : IPersistentJobQueueMonitoringApi
    {
        private readonly EFCoreStorage _storage;

        public EFCoreJobQueueMonitoringApi(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IList<string> GetEnqueuedJobIds([NotNull] string queue, int from, int perPage)
        {
            return GetJobIds(x => x.FetchedAt == null, queue, from, perPage);
        }

        public IList<string> GetFetchedJobIds([NotNull] string queue, int from, int perPage)
        {
            return GetJobIds(x => x.FetchedAt.HasValue, queue, from, perPage);
        }

        private IList<string> GetJobIds(
            Expression<Func<HangfireQueuedJob, bool>> predicate,
            string queue,
            int from,
            int perPage)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            return _storage.UseContext(context => context.Set<HangfireQueuedJob>().
                Where(predicate).
                Where(x => x.Queue == queue).
                OrderBy(x => x.Id).
                Skip(from).
                Take(perPage).
                Select(x => x.JobId).
                AsEnumerable().
                Select(x => x.ToString(CultureInfo.InvariantCulture)).
                ToList());
        }

        public IList<string> GetQueues()
        {
            return _storage.UseContext(context => context.Set<HangfireQueuedJob>().
                Select(x => x.Queue).
                Distinct().
                ToArray());
        }

        public QueueStatisticsDto GetQueueStatistics([NotNull] string queue)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var result = _storage.UseContext(context => (
                from item in context.Set<HangfireQueuedJob>()
                where item.Queue == queue
                group item by item.FetchedAt.HasValue into grouping
                select new
                {
                    grouping.Key,
                    Value = grouping.LongCount(),
                }).
                ToDictionary(x => x.Key, x => x.Value));

            result.TryGetValue(false, out var enqueued);
            result.TryGetValue(true, out var fetched);

            return new QueueStatisticsDto
            {
                Enqueued = enqueued,
                Fetched = fetched,
            };
        }
    }
}
