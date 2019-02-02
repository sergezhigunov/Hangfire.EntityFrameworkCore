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
    internal class EFCoreStorageMonitoringApi : IMonitoringApi
    {
        private const string FailedStatsName = "failed";
        private const string SucceededStatsName = "succeeded";
        private const string DeletedCounterName = "stats:deleted";
        private const string SucceededCounterName = "stats:succeeded";
        private const string RecurringJobsSetName = "recurring-jobs";

        private static IReadOnlyList<string> StatsJobStates { get; } = new[]
        {
            EnqueuedState.StateName,
            ScheduledState.StateName,
            ProcessingState.StateName,
            FailedState.StateName,
        };

        private readonly DbContextOptions _options;

        public EFCoreStorageMonitoringApi(
            DbContextOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public JobList<DeletedJobDto> DeletedJobs(int from, int count)
        {
            return GetJobs(from, count, DeletedState.StateName,
                (invocationData, state) => new DeletedJobDto
                {
                    Job = Deserialize(invocationData),
                    InDeletedState = DeletedState.StateName.Equals(
                        state.Name, StringComparison.OrdinalIgnoreCase),
                    DeletedAt = JobHelper.DeserializeNullableDateTime(
                        state.Data?.GetValue(nameof(DeletedJobDto.DeletedAt))),
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

            var queueProvider = new EFCoreJobQueueProvider(_options);
            var monitoringApi = queueProvider.GetMonitoringApi();
            var statistics = monitoringApi.GetQueueStatistics(queue);
            return statistics.Enqueued;
        }

        public JobList<EnqueuedJobDto> EnqueuedJobs([NotNull] string queue, int from, int perPage)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var queueProvider = new EFCoreJobQueueProvider(_options);
            var monitoringApi = queueProvider.GetMonitoringApi();
            var id = monitoringApi.GetEnqueuedJobIds(queue, from, perPage);
            return EnqueuedJobs(id);
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
                (invocationData, state) => new FailedJobDto
                {
                    Job = Deserialize(invocationData),
                    Reason = state.Reason,
                    ExceptionDetails = state.Data?.GetValue(nameof(FailedJobDto.ExceptionDetails)),
                    ExceptionMessage = state.Data?.GetValue(nameof(FailedJobDto.ExceptionMessage)),
                    ExceptionType = state.Data?.GetValue(nameof(FailedJobDto.ExceptionType)),
                    InFailedState = FailedState.StateName.Equals(
                        state.Name, StringComparison.OrdinalIgnoreCase),
                    FailedAt = JobHelper.DeserializeNullableDateTime(
                        state.Data?.GetValue(nameof(FailedJobDto.FailedAt))),
                });
        }

        public long FetchedCount([NotNull] string queue)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var queueProvider = new EFCoreJobQueueProvider(_options);
            var monitoringApi = queueProvider.GetMonitoringApi();
            var statistics = monitoringApi.GetQueueStatistics(queue);
            return statistics.Fetched;
        }

        public JobList<FetchedJobDto> FetchedJobs([NotNull] string queue, int from, int perPage)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            var queueProvider = new EFCoreJobQueueProvider(_options);
            var monitoringApi = queueProvider.GetMonitoringApi();
            var ids =
                monitoringApi.GetFetchedJobIds(queue, from, perPage).
                ToDictionary(
                    x => long.Parse(x, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    x => x);

            var jobs = _options.UseContext(context =>
            {
                return (
                    from job in context.Jobs
                    where ids.Keys.Contains(job.Id)
                    let actualState = job.ActualState
                    let state = actualState.State
                    select new
                    {
                        job.Id,
                        job.InvocationData,
                        state.Name,
                        FetchedAt = job.Queues.Max(x => x.FetchedAt),
                    }).
                    ToArray();
            });

            return new JobList<FetchedJobDto>(
                jobs.Select(x => new KeyValuePair<string, FetchedJobDto>(
                    ids[x.Id], new FetchedJobDto
                    {
                        Job = Deserialize(x.InvocationData),
                        State = x.Name,
                        FetchedAt = x.FetchedAt,
                    })));
        }

        public StatisticsDto GetStatistics()
        {
            var result = _options.UseContext(context =>
            {
                var stateCounts = (
                    from jobState in context.JobStates
                    where StatsJobStates.Contains(jobState.Name)
                    group jobState by jobState.Name into grouping
                    select new
                    {
                        State = grouping.Key,
                        Count = grouping.LongCount(),
                    }).
                    ToDictionary(x => x.State, x => x.Count);

                var counters = (
                    from counter in context.Counters
                    where
                        counter.Key == SucceededCounterName ||
                        counter.Key == DeletedCounterName
                    group counter by counter.Key into @group
                    select new
                    {
                        CounterName = @group.Key,
                        Sum = @group.Sum(x => x.Value),
                    }).
                    ToDictionary(x => x.CounterName, x => x.Sum);

                return new StatisticsDto
                {
                    Recurring = context.Sets.LongCount(x => x.Key == RecurringJobsSetName),
                    Servers = context.Servers.LongCount(),
                    Enqueued = stateCounts.GetValue(EnqueuedState.StateName),
                    Failed = stateCounts.GetValue(FailedState.StateName),
                    Processing = stateCounts.GetValue(ProcessingState.StateName),
                    Scheduled = stateCounts.GetValue(ScheduledState.StateName),
                    Deleted = counters.GetValue(DeletedCounterName),
                    Succeeded = counters.GetValue(SucceededCounterName),
                };
            });


            var provider = new EFCoreJobQueueProvider(_options);

            result.Queues = (
                from queue in provider.
                    GetMonitoringApi().
                    GetQueues()
                select queue).
                Count();

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

            return _options.UseContext(context =>
            {
                var jobs = context.Jobs.
                    Where(x => x.Id == id);

                var jobInfo = (
                    from job in context.Jobs
                    where job.Id == id
                    select new
                    {
                        job.CreatedAt,
                        job.ExpireAt,
                        job.InvocationData,
                    }).
                    SingleOrDefault();

                if (jobInfo == null)
                    return null;

                var parameters = (
                    from parameter in context.JobParameters
                    where parameter.JobId == id
                    select new
                    {
                        parameter.Name,
                        parameter.Value,
                    }).
                    ToDictionary(x => x.Name, x => x.Value);

                var states = (
                    from state in context.States
                    where state.JobId == id
                    orderby state.Id descending
                    select new StateHistoryDto
                    {
                        CreatedAt = state.CreatedAt,
                        Reason = state.Reason,
                        StateName = state.Name,
                        Data = state.Data,
                    }).
                    ToList();

                return new JobDetailsDto
                {
                    CreatedAt = jobInfo.CreatedAt,
                    ExpireAt = jobInfo.ExpireAt,
                    Job = Deserialize(jobInfo.InvocationData),
                    Properties = parameters,
                    History = states,
                };

            });
        }

        public long ProcessingCount()
        {
            return GetNumberOfJobsByStateName(ProcessingState.StateName);
        }

        public JobList<ProcessingJobDto> ProcessingJobs(int from, int count)
        {
            return GetJobs(from, count, ProcessingState.StateName,
                (invocationData, state) => new ProcessingJobDto
                {
                    Job = Deserialize(invocationData),
                    ServerId = state.Data?.GetValue(nameof(ProcessingJobDto.ServerId)),
                    StartedAt = JobHelper.DeserializeNullableDateTime(
                        state.Data?.GetValue(nameof(ProcessingJobDto.StartedAt))),
                    InProcessingState = ProcessingState.StateName.Equals(
                        state.Name, StringComparison.OrdinalIgnoreCase),
                });
        }

        public IList<QueueWithTopEnqueuedJobsDto> Queues()
        {
            var queueProvider = new EFCoreJobQueueProvider(_options);
            var tuples = (
                from provider in Enumerable.Repeat(queueProvider, 1)
                let monitoring = provider.GetMonitoringApi()
                from queue in monitoring.GetQueues()
                select new
                {
                    Queue = queue,
                    Monitoring = monitoring,
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
                (invocationData, state) => new ScheduledJobDto
                {
                    Job = Deserialize(invocationData),
                    EnqueueAt = JobHelper.DeserializeNullableDateTime(
                        state.Data?.GetValue(nameof(ScheduledJobDto.EnqueueAt))) ?? default,
                    ScheduledAt = JobHelper.DeserializeNullableDateTime(
                        state.Data?.GetValue(nameof(ScheduledJobDto.ScheduledAt))) ?? default,
                    InScheduledState = ScheduledState.StateName.Equals(
                        state.Name, StringComparison.OrdinalIgnoreCase),
                });
        }

        public IList<ServerDto> Servers()
        {
            return _options.UseContext(context => (
                from server in context.Servers
                select new ServerDto
                {
                    Name = server.Id,
                    Heartbeat = server.Heartbeat,
                    Queues = server.Queues,
                    StartedAt = server.StartedAt,
                    WorkersCount = server.WorkerCount,
                }).
                ToList());
        }

        public IDictionary<DateTime, long> SucceededByDatesCount()
        {
            return GetDailyTimelineStats(SucceededStatsName);
        }

        public JobList<SucceededJobDto> SucceededJobs(int from, int count)
        {
            return GetJobs(from, count, SucceededState.StateName,
                (invocationData, state) => new SucceededJobDto
                {
                    Job = Deserialize(invocationData),
                    SucceededAt = JobHelper.DeserializeNullableDateTime(
                        state.Data?.GetValue(nameof(SucceededJobDto.SucceededAt))),
                    Result = state.Data?.GetValue(nameof(SucceededJobDto.Result)),
                    TotalDuration = (
                        state.Data?.ContainsKey("PerformanceDuration") == true &&
                        state.Data?.ContainsKey("Latency") == true)  ?
                        long.Parse(
                            state.Data["PerformanceDuration"],
                            CultureInfo.InvariantCulture) +
                        long.Parse(
                            state.Data["Latency"],
                            CultureInfo.InvariantCulture) :
                        default(long?),
                    InSucceededState = SucceededState.StateName.Equals(
                        state.Name, StringComparison.OrdinalIgnoreCase),
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

        private JobList<EnqueuedJobDto> EnqueuedJobs(IList<string> id)
        {
            var idMap = id.ToDictionary(
                x => long.Parse(x, NumberStyles.Integer, CultureInfo.InvariantCulture),
                x => x);

            var jobs = _options.UseContext(context =>
            {
                return (
                    from job in context.Jobs
                    where idMap.Keys.Contains(job.Id)
                    let actualState = job.ActualState
                    let state = actualState.State
                    select new
                    {
                        job.Id,
                        job.InvocationData,
                        state.Name,
                        state.Data,
                    }).
                    ToArray();
            });

            return new JobList<EnqueuedJobDto>(
                jobs.Select(x => new KeyValuePair<string, EnqueuedJobDto>(
                    idMap[x.Id], new EnqueuedJobDto
                    {
                        Job = Deserialize(x.InvocationData),
                        State = x.Name,
                        InEnqueuedState = EnqueuedState.StateName.Equals(
                            x.Name,
                            StringComparison.OrdinalIgnoreCase),
                        EnqueuedAt = JobHelper.DeserializeNullableDateTime(
                            x.Data?.GetValue(nameof(EnqueuedJobDto.EnqueuedAt)))
                    })));
        }

        private JobList<T> GetJobs<T>(
            int from,
            int count,
            string stateName,
            Func<InvocationData, HangfireState, T> selector)
        {
            return _options.UseContext(context =>
            {
                var items = (
                    from jobState in context.JobStates
                    where jobState.Name == stateName
                    orderby jobState.JobId descending
                    select new
                    {
                        jobState.Job.InvocationData,
                        jobState.State,
                    }).
                    Skip(from).
                    Take(count).
                    ToDictionary(
                        x => x.State.JobId.ToString(CultureInfo.InvariantCulture),
                        x => selector(x.InvocationData, x.State));

                return new JobList<T>(items);
            });
        }

        private long GetNumberOfJobsByStateName(string state)
        {
            return _options.UseContext(context =>
                context.JobStates.LongCount(x => x.Name == state));
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
            var valuesMap = _options.UseContext(context => (
                from counter in context.Counters
                where keyMaps.Keys.Contains(counter.Key)
                group counter by counter.Key into groupByKey
                select new
                {
                    groupByKey.Key,
                    Count = groupByKey.Sum(x => x.Value),
                }).
                ToDictionary(x => x.Key, x => x.Count));

            foreach (var key in keyMaps.Keys)
                if (!valuesMap.ContainsKey(key))
                    valuesMap.Add(key, 0);

            return keyMaps.ToDictionary(x => x.Value, x => valuesMap[x.Key]);
        }

        private static bool TryParseJobId(string jobId, out long id) =>
            long.TryParse(jobId, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }
}
