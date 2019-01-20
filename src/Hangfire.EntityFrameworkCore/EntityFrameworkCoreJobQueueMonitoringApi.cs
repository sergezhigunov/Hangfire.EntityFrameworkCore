using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Hangfire.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EntityFrameworkCoreJobQueueMonitoringApi : IPersistentJobQueueMonitoringApi
    {
        private readonly DbContextOptions<HangfireContext> _options;

        public EntityFrameworkCoreJobQueueMonitoringApi(DbContextOptions<HangfireContext> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
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
            Expression<Func<HangfireJobQueue, bool>> predicate,
            string queue,
            int from,
            int perPage)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            return _options.UseContext(context => context.JobQueues.
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
            return _options.UseContext(context => context.JobQueues.
                Select(x => x.Queue).
                Distinct().
                ToArray());
        }

        public QueueStatisticsDto GetQueueStatistics([NotNull] string queue)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var result = _options.UseContext(context => (
                from item in context.JobQueues
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
