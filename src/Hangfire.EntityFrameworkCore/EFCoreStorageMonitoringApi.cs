using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    using GetCountersFunc = Func<HangfireContext, ICollection<string>, IEnumerable<KeyValuePair<string, long>>>;
    using GetJobDetailsFunc = Func<HangfireContext, long, JobDetailsDto>;
    using GetJobParametersFunc = Func<HangfireContext, long, IEnumerable<KeyValuePair<string, string>>>;
    using GetStateDataFunc = Func<HangfireContext, string, int, int, IEnumerable<EFCoreStorageMonitoringApi.JobState>>;
    using GetStateHistoryFunc = Func<HangfireContext, long, IEnumerable<StateHistoryDto>>;
    using GetStateCountFunc = Func<HangfireContext, string, long>;
    using GetStatisticsFunc = Func<HangfireContext, StatisticsDto>;
    using EnqueuedJobsFunc = Func<HangfireContext, IEnumerable<long>, IEnumerable<KeyValuePair<string, EnqueuedJobDto>>>;
    using FetchedJobsFunc = Func<HangfireContext, IEnumerable<long>, IEnumerable<KeyValuePair<string, FetchedJobDto>>>;
    using ServersFunc = Func<HangfireContext, IEnumerable<ServerDto>>;

    internal class EFCoreStorageMonitoringApi : IMonitoringApi
    {
        private const string FailedStatsName = "failed";
        private const string SucceededStatsName = "succeeded";
        private const string DeletedCounterName = "stats:deleted";
        private const string SucceededCounterName = "stats:succeeded";
        private const string RecurringJobsSetName = "recurring-jobs";

        private static GetCountersFunc GetCountersFunc { get; } = EF.CompileQuery(
            (HangfireContext context, IEnumerable<string> keys) =>
                from x in context.Set<HangfireCounter>()
                where keys.Contains(x.Key)
                group x.Value by x.Key into x
                select new KeyValuePair<string, long>(x.Key, x.Sum()));

        private static GetJobDetailsFunc GetJobDetailsFunc { get; } = EF.CompileQuery(
            (HangfireContext context, long id) => (
                from x in context.Set<HangfireJob>()
                where x.Id == id
                select new JobDetailsDto
                {
                    CreatedAt = x.CreatedAt,
                    ExpireAt = x.ExpireAt,
                    Job = Deserialize(x.InvocationData),
                }).
                SingleOrDefault());

        private static GetJobParametersFunc GetJobParametersFunc { get; } = EF.CompileQuery(
            (HangfireContext context, long id) =>
                from x in context.Set<HangfireJobParameter>()
                where x.JobId == id
                select new KeyValuePair<string, string>(x.Name, x.Value));

        private static GetStateDataFunc GetStateDataFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string name, int from, int count) => (
                from x in context.Set<HangfireJobState>()
                where x.Name == name
                let s = x.State
                orderby x.JobId descending
                select new JobState
                {
                    Id = x.JobId,
                    Job = Deserialize(x.Job.InvocationData),
                    Reason = s.Reason,
                    Data = s.Data,
                }).
                Skip(from).
                Take(count));

        private static GetStateHistoryFunc GetStateHistoryFunc { get; } = EF.CompileQuery(
            (HangfireContext context, long id) =>
                from x in context.Set<HangfireState>()
                where x.JobId == id
                select new StateHistoryDto
                {
                    CreatedAt = x.CreatedAt,
                    Reason = x.Reason,
                    StateName = x.Name,
                    Data = x.Data,
                });

        private static GetStateCountFunc GetStateCountFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string name) =>
                context.Set<HangfireJobState>().
                LongCount(x => x.Name == name));

        private static GetStatisticsFunc GetStatisticsFunc { get; } = EF.CompileQuery(
            (HangfireContext context) => (
                from stub in Enumerable.Repeat(0, 1)
                let sets = context.Set<HangfireSet>().AsQueryable()
                let jobStates = context.Set<HangfireJobState>().AsQueryable()
                let counters = context.Set<HangfireCounter>().AsQueryable()
                let deletedCounters = counters.Where(x => x.Key == DeletedCounterName)
                let succeededCounters = counters.Where(x => x.Key == SucceededCounterName)
                select new StatisticsDto
                {
                    Recurring = sets.LongCount(x => x.Key == RecurringJobsSetName),
                    Servers = context.Set<HangfireServer>().LongCount(),
                    Enqueued = jobStates.LongCount(x => x.Name == EnqueuedState.StateName),
                    Failed = jobStates.LongCount(x => x.Name == FailedState.StateName),
                    Processing = jobStates.LongCount(x => x.Name == ProcessingState.StateName),
                    Scheduled = jobStates.LongCount(x => x.Name == ScheduledState.StateName),
                    Deleted = deletedCounters.Sum(x => x.Value),
                    Succeeded = succeededCounters.Sum(x => x.Value),
                }).
                FirstOrDefault());

        private static EnqueuedJobsFunc EnqueuedJobsFunc { get; } = EF.CompileQuery(
            (HangfireContext context, IEnumerable<long> keys) =>
                from jobState in context.Set<HangfireJobState>()
                let id = jobState.JobId
                where keys.Contains(id)
                let state = jobState.State
                let job = jobState.Job
                select new KeyValuePair<string, EnqueuedJobDto>(
                    id.ToString(CultureInfo.InvariantCulture),
                    new EnqueuedJobDto
                    {
                        Job = Deserialize(job.InvocationData),
                        State = jobState.Name,
                        InEnqueuedState = EnqueuedState.StateName.Equals(jobState.Name,
                            StringComparison.OrdinalIgnoreCase),
                        EnqueuedAt = JobHelper.DeserializeNullableDateTime(
                            state.Data.GetValue(nameof(EnqueuedJobDto.EnqueuedAt))),
                    }));

        private static FetchedJobsFunc FetchedJobsFunc { get; } = EF.CompileQuery(
            (HangfireContext context, IEnumerable<long> keys) =>
                from jobState in context.Set<HangfireJobState>()
                let id = jobState.JobId
                where keys.Contains(id)
                let state = jobState.State
                let job = jobState.Job
                select new KeyValuePair<string, FetchedJobDto>(
                    id.ToString(CultureInfo.InvariantCulture),
                    new FetchedJobDto
                    {
                        Job = Deserialize(job.InvocationData),
                        State = jobState.Name,
                        FetchedAt = job.QueuedJobs.Max(x => x.FetchedAt),
                    }));

        private static ServersFunc ServersFunc { get; } = EF.CompileQuery(
            (HangfireContext context) =>
                from server in context.Set<HangfireServer>()
                select new ServerDto
                {
                    Name = server.Id,
                    Heartbeat = server.Heartbeat,
                    Queues = server.Queues,
                    StartedAt = server.StartedAt,
                    WorkersCount = server.WorkerCount,
                });

        private readonly EFCoreStorage _storage;

        public EFCoreStorageMonitoringApi(
            EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public JobList<DeletedJobDto> DeletedJobs(int from, int count)
        {
            return GetJobs(from, count, DeletedState.StateName,
                (job, data, reason) => new DeletedJobDto
                {
                    Job = job,
                    DeletedAt = JobHelper.DeserializeNullableDateTime(
                        data?.GetValue(nameof(DeletedJobDto.DeletedAt))),
                });
        }

        public long DeletedListCount()
        {
            return GetNumberOfJobsByStateName(DeletedState.StateName);
        }

        public long EnqueuedCount([NotNull] string queue)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var provider = _storage.GetQueueProvider(queue);
            var monitoringApi = provider.GetMonitoringApi();
            var statistics = monitoringApi.GetQueueStatistics(queue);
            return statistics.Enqueued;
        }

        public JobList<EnqueuedJobDto> EnqueuedJobs([NotNull] string queue, int from, int perPage)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var provider = _storage.GetQueueProvider(queue);
            var monitoringApi = provider.GetMonitoringApi();
            var ids = monitoringApi.GetEnqueuedJobIds(queue, from, perPage);
            return EnqueuedJobs(ids);
        }

        public IDictionary<DateTime, long> FailedByDatesCount()
        {
            return GetDailyTimelineStats(FailedStatsName);
        }

        public long FailedCount()
        {
            return GetNumberOfJobsByStateName(FailedState.StateName);
        }

        public JobList<FailedJobDto> FailedJobs(int from, int count)
        {
            return GetJobs(from, count, FailedState.StateName,
                (job, data, reason) => new FailedJobDto
                {
                    Job = job,
                    Reason = reason,
                    ExceptionDetails = data?.GetValue(nameof(FailedJobDto.ExceptionDetails)),
                    ExceptionMessage = data?.GetValue(nameof(FailedJobDto.ExceptionMessage)),
                    ExceptionType = data?.GetValue(nameof(FailedJobDto.ExceptionType)),
                    FailedAt = JobHelper.DeserializeNullableDateTime(
                        data?.GetValue(nameof(FailedJobDto.FailedAt))),
                });
        }

        public long FetchedCount([NotNull] string queue)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var provider = _storage.GetQueueProvider(queue);
            var monitoringApi = provider.GetMonitoringApi();
            var statistics = monitoringApi.GetQueueStatistics(queue);
            return statistics.Fetched;
        }

        public JobList<FetchedJobDto> FetchedJobs([NotNull] string queue, int from, int perPage)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var provider = _storage.GetQueueProvider(queue);
            var monitoringApi = provider.GetMonitoringApi();
            var ids = monitoringApi.GetFetchedJobIds(queue, from, perPage);

            if (!ids.Any())
                return new JobList<FetchedJobDto>(
                    Array.Empty<KeyValuePair<string, FetchedJobDto>>());

            var keys = ids.Select(x =>
                long.Parse(x, NumberStyles.Integer, CultureInfo.InvariantCulture));

            return new JobList<FetchedJobDto>(_storage.UseContext(context =>
                FetchedJobsFunc(context, keys)));
        }

        public StatisticsDto GetStatistics()
        {
            var result = _storage.UseContext(GetStatisticsFunc);

            result.Queues = _storage.DefaultQueueProvider.
                GetMonitoringApi().
                GetQueues().
                Union(_storage.QueueProviders.Keys, StringComparer.OrdinalIgnoreCase).
                LongCount();

            return result;
        }

        public IDictionary<DateTime, long> HourlyFailedJobs()
        {
            return GetHourlyTimelineStats(FailedStatsName);
        }

        public IDictionary<DateTime, long> HourlySucceededJobs()
        {
            return GetHourlyTimelineStats(SucceededStatsName);
        }

        public JobDetailsDto JobDetails([NotNull] string jobId)
        {
            if (jobId == null)
                throw new ArgumentNullException(nameof(jobId));

            if (!TryParseJobId(jobId, out var id))
                return null;

            return _storage.UseContext(context =>
            {
                var jobInfo = GetJobDetailsFunc(context, id);

                if (jobInfo == null)
                    return null;

                jobInfo.Properties = GetJobParametersFunc(context, id).
                    ToDictionary(x => x.Key, x => x.Value);
                jobInfo.History = GetStateHistoryFunc(context, id).
                    ToList();

                return jobInfo;
            });
        }

        public long ProcessingCount()
        {
            return GetNumberOfJobsByStateName(ProcessingState.StateName);
        }

        public JobList<ProcessingJobDto> ProcessingJobs(int from, int count)
        {
            return GetJobs(from, count, ProcessingState.StateName,
                (job, data, reason) => new ProcessingJobDto
                {
                    Job = job,
                    ServerId = data?.GetValue(nameof(ProcessingJobDto.ServerId)),
                    StartedAt = JobHelper.DeserializeNullableDateTime(
                        data?.GetValue(nameof(ProcessingJobDto.StartedAt))),
                });
        }

        public IList<QueueWithTopEnqueuedJobsDto> Queues()
        {
            var tuples = (
                from provider in Enumerable.Repeat(_storage.DefaultQueueProvider, 1)
                let monitoring = provider.GetMonitoringApi()
                from queue in monitoring.GetQueues()
                select new
                {
                    Queue = queue,
                    Monitoring = monitoring,
                }).
                Concat(
                    from item in _storage.QueueProviders
                    select new
                    {
                        Queue = item.Key,
                        Monitoring = item.Value.GetMonitoringApi(),
                    }).
                ToArray();

            var result = new List<QueueWithTopEnqueuedJobsDto>(tuples.Length);

            foreach (var tuple in tuples)
            {
                var enqueuedJobIds = tuple.Monitoring.GetEnqueuedJobIds(tuple.Queue, 0, 5);
                var enqueuedJobCount = tuple.Monitoring.GetQueueStatistics(tuple.Queue);
                var firstJobs = EnqueuedJobs(enqueuedJobIds);

                result.Add(new QueueWithTopEnqueuedJobsDto
                {
                    Name = tuple.Queue,
                    Length = enqueuedJobCount.Enqueued,
                    Fetched = enqueuedJobCount.Fetched,
                    FirstJobs = firstJobs
                });
            }

            return result;
        }

        public long ScheduledCount()
        {
            return GetNumberOfJobsByStateName(ScheduledState.StateName);
        }

        public JobList<ScheduledJobDto> ScheduledJobs(int from, int count)
        {
            return GetJobs(from, count, ScheduledState.StateName,
                (job, data, reason) => new ScheduledJobDto
                {
                    Job = job,
                    EnqueueAt = JobHelper.DeserializeNullableDateTime(
                        data?.GetValue(nameof(ScheduledJobDto.EnqueueAt))) ?? default,
                    ScheduledAt = JobHelper.DeserializeNullableDateTime(
                        data?.GetValue(nameof(ScheduledJobDto.ScheduledAt))) ?? default,
                });
        }

        public IList<ServerDto> Servers() => _storage.UseContext(ServersFunc).ToList();

        public IDictionary<DateTime, long> SucceededByDatesCount()
        {
            return GetDailyTimelineStats(SucceededStatsName);
        }

        public JobList<SucceededJobDto> SucceededJobs(int from, int count)
        {
            return GetJobs(from, count, SucceededState.StateName,
                (job, data, reason) => new SucceededJobDto
                {
                    Job = job,
                    SucceededAt = JobHelper.DeserializeNullableDateTime(
                        data?.GetValue(nameof(SucceededJobDto.SucceededAt))),
                    Result = data?.GetValue(nameof(SucceededJobDto.Result)),
                    TotalDuration = data == null ? default :
                        data.TryGetValue("PerformanceDuration", out var duration) &&
                            data.TryGetValue("Latency", out var latency) ?
                            long.Parse(duration, CultureInfo.InvariantCulture) +
                                long.Parse(latency, CultureInfo.InvariantCulture) :
                        default(long?)
                });
        }

        public long SucceededListCount()
        {
            return GetNumberOfJobsByStateName(SucceededState.StateName);
        }

        private static Job Deserialize(InvocationData data)
        {
            try
            {
                return data.Deserialize();
            }
            catch (JobLoadException)
            {
                return null;
            }
        }

        private JobList<EnqueuedJobDto> EnqueuedJobs(IList<string> ids)
        {
            if (!ids.Any())
                return new JobList<EnqueuedJobDto>(
                    Array.Empty<KeyValuePair<string, EnqueuedJobDto>>());

            var keys = ids.Select(
                x => long.Parse(x, NumberStyles.Integer, CultureInfo.InvariantCulture));

            return new JobList<EnqueuedJobDto>(_storage.UseContext(context =>
                EnqueuedJobsFunc(context, keys)));
        }

        private JobList<T> GetJobs<T>(int from, int count, string stateName,
            Func<Job, IDictionary<string, string>, string, T> selector)
        {
            return _storage.UseContext(context =>
                new JobList<T>(GetStateDataFunc(context, stateName, from, count).
                    ToDictionary(
                        x => x.Id.ToString(CultureInfo.InvariantCulture),
                        x => selector(x.Job, x.Data, x.Reason))));
        }

        private long GetNumberOfJobsByStateName(string state)
        {
            return _storage.UseContext(context => GetStateCountFunc(context, state));
        }

        private Dictionary<DateTime, long> GetHourlyTimelineStats(string type)
        {
            var ticks = DateTime.UtcNow.Ticks;
            var endDate = new DateTime(ticks - ticks % TimeSpan.TicksPerHour, DateTimeKind.Utc);
            var dates = Enumerable.Range(0, 24).Select(x => endDate.AddHours(-x));
            var keyMaps = dates.ToDictionary(x => $"stats:{type}:{x:yyyy-MM-dd-HH}");

            return GetTimelineStats(keyMaps);
        }

        private Dictionary<DateTime, long> GetDailyTimelineStats(string type)
        {
            var endDate = DateTime.UtcNow.Date;
            var dates = Enumerable.Range(0, 7).Select(x => endDate.AddDays(-x));
            var keyMaps = dates.ToDictionary(x => $"stats:{type}:{x:yyyy-MM-dd}");

            return GetTimelineStats(keyMaps);
        }

        private Dictionary<DateTime, long> GetTimelineStats(IDictionary<string, DateTime> keyMaps)
        {
            var counters = _storage.UseContext(context => GetCountersFunc(context, keyMaps.Keys)).
                ToDictionary(x => x.Key, x => x.Value);

            return keyMaps.ToDictionary(x => x.Value,
                x => counters.TryGetValue(x.Key, out var result) ? result : 0L);
        }

        private static bool TryParseJobId(string jobId, out long id) =>
            long.TryParse(jobId, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

        internal class JobState
        {
            public long Id { get; internal set; }
            public Job Job { get; internal set; }
            public string Reason { get; internal set; }
            public IDictionary<string, string> Data { get; internal set; }
        }
    }
}
