using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Hangfire.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    using GetCountFunc = Func<HangfireContext, string, long>;
    using GetQueuesFunc = Func<HangfireContext, IEnumerable<string>>;
    using GetJobIdsFunc = Func<HangfireContext, string, int, int, IEnumerable<long>>;
    using QueuedJobPredicate = Expression<Func<HangfireQueuedJob, bool>>;

    internal sealed class EFCoreJobQueueMonitoringApi : IPersistentJobQueueMonitoringApi
    {
        private readonly EFCoreStorage _storage;

        private static QueuedJobPredicate EnqueuedPredicate { get; } = x => x.FetchedAt == null;

        private static QueuedJobPredicate FetchedPredicate { get; } = x => x.FetchedAt.HasValue;

        private static GetCountFunc GetEnqueuedCountFunc { get; } = EF.CompileQuery(
            GetCountExpression(EnqueuedPredicate));

        private static GetCountFunc GetFetchedCountFunc { get; } = EF.CompileQuery(
            GetCountExpression(FetchedPredicate));

        private static GetJobIdsFunc GetEnqueuedJobIdsFunc { get; } = EF.CompileQuery(
            GetJobIdsExpression(EnqueuedPredicate));

        private static GetJobIdsFunc GetFetchedJobIdsFunc { get; } = EF.CompileQuery(
            GetJobIdsExpression(FetchedPredicate));

        private static GetQueuesFunc GetQueuesFunc { get; } = EF.CompileQuery(
            (HangfireContext context) =>
                context.Set<HangfireQueuedJob>().
                Select(x => x.Queue).
                Distinct());

        public EFCoreJobQueueMonitoringApi(EFCoreStorage storage)
        {
            if (storage is null)
                throw new ArgumentNullException(nameof(storage));

            _storage = storage;
        }

        public IList<string> GetEnqueuedJobIds([NotNull] string queue, int from, int perPage)
        {
            if (queue is null)
                throw new ArgumentNullException(nameof(queue));

            return _storage.
                UseContext(context =>
                    GetEnqueuedJobIdsFunc(context, queue, from, perPage).
                    ToList()).
                Select(x => x.ToString(CultureInfo.InvariantCulture)).
                ToList();
        }

        public IList<string> GetFetchedJobIds([NotNull] string queue, int from, int perPage)
        {
            if (queue is null)
                throw new ArgumentNullException(nameof(queue));

            return _storage.
                UseContext(context =>
                    GetFetchedJobIdsFunc(context, queue, from, perPage).
                    ToList()).
                Select(x => x.ToString(CultureInfo.InvariantCulture)).
                ToList();
        }

        public IList<string> GetQueues() =>
            _storage.UseContext(context =>
                GetQueuesFunc(context).
                ToList());

        public QueueStatisticsDto GetQueueStatistics([NotNull] string queue)
        {
            return UseContext(
                (c, q) => new QueueStatisticsDto
                {
                    Enqueued = GetEnqueuedCountFunc(c, q),
                    Fetched = GetFetchedCountFunc(c, q),
                },
                queue);
        }

        private T UseContext<T>(Func<HangfireContext, string, T> func, string queue)
        {
            if (queue is null)
                throw new ArgumentNullException(nameof(queue));

            return _storage.UseContext(context => func(context, queue));
        }

        private static Expression<GetCountFunc> GetCountExpression(QueuedJobPredicate predicate)
            => (HangfireContext context, string queue) => context.Set<HangfireQueuedJob>().
                Where(predicate).
                Where(x => x.Queue == queue).
                LongCount();

        private static Expression<GetJobIdsFunc> GetJobIdsExpression(QueuedJobPredicate predicate)
            => (HangfireContext context, string queue, int from, int perPage) => (
                from x in context.Set<HangfireQueuedJob>().Where(predicate)
                where x.Queue == queue
                orderby x.Id ascending
                select x.Id).
                Skip(from).
                Take(perPage);
    }
}
