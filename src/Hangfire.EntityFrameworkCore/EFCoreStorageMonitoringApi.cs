using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Hangfire.States;
using Hangfire.Storage.Monitoring;

namespace Hangfire.EntityFrameworkCore;

using GetCountersFunc = Func<DbContext, ICollection<string>, IEnumerable<KeyValuePair<string, long>>>;
using GetJobDetailsFunc = Func<DbContext, long, JobDetailsDto>;
using GetJobParametersFunc = Func<DbContext, long, IEnumerable<KeyValuePair<string, string>>>;
using GetStateDataFunc = Func<DbContext, string, int, int, IEnumerable<EFCoreStorageMonitoringApi.JobState>>;
using GetStateHistoryFunc = Func<DbContext, long, IEnumerable<StateHistoryDto>>;
using GetStateCountFunc = Func<DbContext, string, long>;
using GetStatisticsFunc = Func<DbContext, IEnumerable<KeyValuePair<string, long>>>;
using GetCountFunc = Func<DbContext, long>;
using EnqueuedJobsFunc = Func<DbContext, IEnumerable<long>, IEnumerable<EFCoreStorageMonitoringApi.EnqueuedJobState>>;
using FetchedJobsFunc = Func<DbContext, IEnumerable<long>, IEnumerable<KeyValuePair<long, FetchedJobDto>>>;
using ServersFunc = Func<DbContext, IEnumerable<ServerDto>>;
using NotNullAttribute = Annotations.NotNullAttribute;

internal class EFCoreStorageMonitoringApi : IMonitoringApi
{
    private const string FailedStatsName = "failed";
    private const string SucceededStatsName = "succeeded";
    private const string DeletedCounterName = "stats:deleted";
    private const string SucceededCounterName = "stats:succeeded";
    private const string RecurringJobsSetName = "recurring-jobs";

    private static GetCountersFunc GetCountersFunc { get; } = EF.CompileQuery(
        (DbContext context, IEnumerable<string> keys) =>
            from x in context.Set<HangfireCounter>()
            where keys.Contains(x.Key)
            group x.Value by x.Key into x
            select new KeyValuePair<string, long>(x.Key, x.Sum()));

    private static GetJobDetailsFunc GetJobDetailsFunc { get; } = EF.CompileQuery(
        (DbContext context, long id) => (
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
        (DbContext context, long id) =>
            from x in context.Set<HangfireJobParameter>()
            where x.JobId == id
            select new KeyValuePair<string, string>(x.Name, x.Value));

    private static GetStateDataFunc GetStateDataFunc { get; } = EF.CompileQuery(
        (DbContext context, string name, int from, int count) => (
            from x in context.Set<HangfireJob>()
            where x.StateName == name
            let s = x.State
            orderby x.Id descending
            select new JobState
            {
                Id = x.Id,
                Job = Deserialize(x.InvocationData),
                Reason = s.Reason,
                Data = s.Data,
            }).
            Skip(from).
            Take(count));

    private static GetStateHistoryFunc GetStateHistoryFunc { get; } = EF.CompileQuery(
        (DbContext context, long id) =>
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
        (DbContext context, string name) =>
            context.Set<HangfireJob>().
            LongCount(x => x.StateName != null && x.StateName == name));

    private static GetStatisticsFunc GetJobStatisticsFunc { get; } = EF.CompileQuery(
        (DbContext context) => (
            from x in context.Set<HangfireJob>()
            where new[]
            {
                    EnqueuedState.StateName,
                    FailedState.StateName,
                    ProcessingState.StateName,
                    ScheduledState.StateName,
            }.
            Contains(x.StateName)
            group x by x.StateName into g
            select new KeyValuePair<string, long>(g.Key, g.LongCount())));

    private static GetStatisticsFunc GetCounterStatisticsFunc { get; } = EF.CompileQuery(
        (DbContext context) => (
            from x in context.Set<HangfireCounter>()
            where new[]
            {
                    DeletedCounterName,
                    SucceededCounterName,
            }.
            Contains(x.Key)
            group x by x.Key into g
            select new KeyValuePair<string, long>(g.Key, g.Sum(x => x.Value))));

    private static GetCountFunc GetServersCountFunc { get; } = EF.CompileQuery(
        (DbContext context) => (
            from x in context.Set<HangfireServer>()
            select x).
            LongCount());

    private static GetCountFunc GetRecurringJobCountFunc { get; } = EF.CompileQuery(
        (DbContext context) => context.Set<HangfireSet>().
            LongCount(x => x.Key == RecurringJobsSetName));

    private static EnqueuedJobsFunc EnqueuedJobsFunc { get; } = EF.CompileQuery(
        (DbContext context, IEnumerable<long> keys) =>
            from job in context.Set<HangfireJob>()
            let id = job.Id
            where keys.Contains(id)
            let state = job.State
            select new EnqueuedJobState
            {
                Id = id,
                StateName = job.StateName,
                InvocationData = SerializationHelper.Serialize(job.InvocationData),
                StateData = SerializationHelper.Serialize(state.Data),
            });

    private static FetchedJobsFunc FetchedJobsFunc { get; } = EF.CompileQuery(
        (DbContext context, IEnumerable<long> keys) =>
            from job in context.Set<HangfireJob>()
            let id = job.Id
            where keys.Contains(id)
            let state = job.State
            select new KeyValuePair<long, FetchedJobDto>(
                id,
                new FetchedJobDto
                {
                    Job = Deserialize(job.InvocationData),
                    State = job.StateName,
                    FetchedAt = job.QueuedJobs.Max(x => x.FetchedAt),
                }));

    private static ServersFunc ServersFunc { get; } = EF.CompileQuery(
        (DbContext context) =>
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

    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreStorageMonitoringApi(
        EFCoreStorage storage)
    {
        if (storage is null)
            throw new ArgumentNullException(nameof(storage));

        _storage = storage;
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

    [SuppressMessage("Maintainability", "CA1510")]
    public long EnqueuedCount([NotNull] string queue)
    {
        if (queue is null)
            throw new ArgumentNullException(nameof(queue));

        var provider = _storage.GetQueueProvider(queue);
        var monitoringApi = provider.GetMonitoringApi();
        var statistics = monitoringApi.GetQueueStatistics(queue);
        return statistics.Enqueued;
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public JobList<EnqueuedJobDto> EnqueuedJobs([NotNull] string queue, int from, int perPage)
    {
        if (queue is null)
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

    [SuppressMessage("Maintainability", "CA1510")]
    public long FetchedCount([NotNull] string queue)
    {
        if (queue is null)
            throw new ArgumentNullException(nameof(queue));

        var provider = _storage.GetQueueProvider(queue);
        var monitoringApi = provider.GetMonitoringApi();
        var statistics = monitoringApi.GetQueueStatistics(queue);
        return statistics.Fetched;
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public JobList<FetchedJobDto> FetchedJobs([NotNull] string queue, int from, int perPage)
    {
        if (queue is null)
            throw new ArgumentNullException(nameof(queue));

        var provider = _storage.GetQueueProvider(queue);
        var monitoringApi = provider.GetMonitoringApi();
        var ids = monitoringApi.GetFetchedJobIds(queue, from, perPage);

        if (!ids.Any())
            return new JobList<FetchedJobDto>(
                Array.Empty<KeyValuePair<string, FetchedJobDto>>());

        var keys = ids.Select(x =>
            long.Parse(x, NumberStyles.Integer, CultureInfo.InvariantCulture));

        var items = _storage.UseContext(context =>
            FetchedJobsFunc(context, keys).
            ToList());

        return new JobList<FetchedJobDto>(
            items.Select(x => new KeyValuePair<string, FetchedJobDto>(
                x.Key.ToString(CultureInfo.InvariantCulture),
                x.Value)));
    }

    public StatisticsDto GetStatistics()
    {
        var result = _storage.UseContext(context =>
        {
            var dictionary = GetJobStatisticsFunc(context).
                Concat(GetCounterStatisticsFunc(context)).
                ToDictionary(x => x.Key, x => x.Value);

            var recurringCount = GetRecurringJobCountFunc(context);
            var serversCount = GetServersCountFunc(context);
            return new StatisticsDto
            {
                Servers = serversCount,
                Recurring = recurringCount,
                Deleted = dictionary.GetValue(DeletedCounterName),
                Succeeded = dictionary.GetValue(SucceededCounterName),
                Enqueued = dictionary.GetValue(EnqueuedState.StateName),
                Failed = dictionary.GetValue(FailedState.StateName),
                Processing = dictionary.GetValue(ProcessingState.StateName),
                Scheduled = dictionary.GetValue(ScheduledState.StateName),
            };
        });

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

    [SuppressMessage("Maintainability", "CA1510")]
    public JobDetailsDto JobDetails([NotNull] string jobId)
    {
        if (jobId is null)
            throw new ArgumentNullException(nameof(jobId));

        if (!TryParseJobId(jobId, out var id))
            return null;

        return _storage.UseContext(context =>
        {
            var jobInfo = GetJobDetailsFunc(context, id);

            if (jobInfo is null)
                return null;

            jobInfo.Properties = GetJobParametersFunc(context, id).
                ToDictionary(x => x.Key, x => x.Value);
            jobInfo.History = GetStateHistoryFunc(context, id).
                OrderByDescending(x => x.CreatedAt).
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

    public IList<ServerDto> Servers() =>
        _storage.UseContext(context =>
            ServersFunc(context).
            ToList());

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
                TotalDuration = data is null ? default :
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
            return data.DeserializeJob();
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

        var items = _storage.UseContext(context =>
            EnqueuedJobsFunc(context, keys).
            ToList());

        return new JobList<EnqueuedJobDto>(
            items.Select(x => new KeyValuePair<string, EnqueuedJobDto>(
                x.Id.ToString(CultureInfo.InvariantCulture),
                new EnqueuedJobDto
                {
                    Job = Deserialize(SerializationHelper.Deserialize<InvocationData>(x.InvocationData)),
                    State = x.StateName,
                    InEnqueuedState = EnqueuedState.StateName.Equals(
                        x.StateName,
                        StringComparison.OrdinalIgnoreCase),
                    EnqueuedAt = x.StateData is null ? default :
                        JobHelper.DeserializeNullableDateTime(
                            SerializationHelper.Deserialize<Dictionary<string, string>>(x.StateData).
                                GetValue(nameof(EnqueuedJobDto.EnqueuedAt))),
                })));
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
        var counters = _storage.UseContext(context =>
            GetCountersFunc(context, keyMaps.Keys).
            ToDictionary(x => x.Key, x => x.Value));

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

    internal class EnqueuedJobState
    {
        public long Id { get; set; }
        public string StateName { get; set; }
        public string InvocationData { get; set; }
        public string StateData { get; set; }
    }
}
