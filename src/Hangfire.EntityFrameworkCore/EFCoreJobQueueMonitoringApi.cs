using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Hangfire.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    using GetQueuesFunc = Func<HangfireContext, IEnumerable<string>>;
    using GetJobIdsFunc = Func<HangfireContext, string, int, int, IEnumerable<string>>;
    using GetQueueStatisticsFunc = Func<HangfireContext, string, QueueStatisticsDto>;
    using QueuedJobPredicate = Expression<Func<HangfireQueuedJob, bool>>;

    internal sealed class EFCoreJobQueueMonitoringApi : IPersistentJobQueueMonitoringApi
    {
        private readonly EFCoreStorage _storage;

        private static QueuedJobPredicate EnqueuedPredicate { get; } = x => x.FetchedAt == null;

        private static QueuedJobPredicate FetchedPredicate { get; } = x => x.FetchedAt.HasValue;

        private static GetJobIdsFunc GetEnqueuedJobIdsFunc { get; } = EF.CompileQuery(
            GetJobIdsExpression(EnqueuedPredicate));

        private static GetJobIdsFunc GetFetchedJobIdsFunc { get; } = EF.CompileQuery(
            GetJobIdsExpression(FetchedPredicate));

        private static GetQueuesFunc GetQueuesFunc { get; } = EF.CompileQuery(
            (HangfireContext context) =>
                context.Set<HangfireQueuedJob>().
                Select(x => x.Queue).
                Distinct());

        private static GetQueueStatisticsFunc GetQueueStatisticsFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string queue) => (
                from stub in Enumerable.Repeat(0, 1)
                let queuedJobs =
                    from x in context.Set<HangfireQueuedJob>()
                    where x.Queue == queue
                    select x
                select new QueueStatisticsDto
                {
                    Enqueued = queuedJobs.LongCount(EnqueuedPredicate),
                    Fetched = queuedJobs.LongCount(FetchedPredicate)
                }).
                Single());

        public EFCoreJobQueueMonitoringApi(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public IList<string> GetEnqueuedJobIds([NotNull] string queue, int from, int perPage) =>
            UseContext(GetEnqueuedJobIdsFunc, queue, from, perPage).ToList();

        public IList<string> GetFetchedJobIds([NotNull] string queue, int from, int perPage) =>
            UseContext(GetFetchedJobIdsFunc, queue, from, perPage).ToList();

        public IList<string> GetQueues() => _storage.UseContext(GetQueuesFunc).ToList();

        public QueueStatisticsDto GetQueueStatistics([NotNull] string queue) =>
            UseContext(GetQueueStatisticsFunc, queue);

        private T UseContext<T>(Func<HangfireContext, string, T> func, string queue)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            return _storage.UseContext(context => func(context, queue));
        }

        private T UseContext<T>(Func<HangfireContext, string, int, int, T> func,
            string queue, int from, int perPage) =>
            UseContext((c, q) => func(c, queue, from, perPage), queue);

        private static Expression<GetJobIdsFunc> GetJobIdsExpression(QueuedJobPredicate predicate)
            => (HangfireContext context, string queue, int from, int perPage) =>
                context.Set<HangfireQueuedJob>().
                Where(predicate).
                Where(x => x.Queue == queue).
                OrderBy(x => x.Id).
                Skip(from).
                Take(perPage).
                Select(x => x.JobId.ToString(CultureInfo.InvariantCulture));
    }
}
