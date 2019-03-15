using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    using GetAllEntriesFromHashFunc = Func<HangfireContext, string, IEnumerable<KeyValuePair<string, string>>>;
    using GetAllItemsFromListFunc = Func<HangfireContext, string, IEnumerable<string>>;
    using GetAllItemsFromSetFunc = Func<HangfireContext, string, IEnumerable<string>>;
    using GetCounterFunc = Func<HangfireContext, string, long>;
    using GetFirstByLowestScoreFromSetFunc = Func<HangfireContext, string, double, double, string>;
    using GetHashCountFunc = Func<HangfireContext, string, long>;
    using GetHashFieldsFunc = Func<HangfireContext, string, IEnumerable<string>>;
    using GetHashTtlFunc = Func<HangfireContext, string, DateTime?>;
    using GetJobDataFunc = Func<HangfireContext, long, JobData>;
    using GetJobParameterFunc = Func<HangfireContext, long, string, string>;
    using GetListCountFunc = Func<HangfireContext, string, long>;
    using GetListTtlFunc = Func<HangfireContext, string, DateTime?>;
    using GetRangeFromListFunc = Func<HangfireContext, string, int, int, IEnumerable<string>>;
    using GetRangeFromSetFunc = Func<HangfireContext, string, int, int, IEnumerable<string>>;
    using GetSetCountFunc = Func<HangfireContext, string, long>;
    using GetSetTtlFunc = Func<HangfireContext, string, DateTime?>;
    using GetStateDataFunc = Func<HangfireContext, long, StateData>;
    using GetTimedOutServersFunc = Func<HangfireContext, DateTime, IEnumerable<string>>;
    using GetValueFromHashFunc = Func<HangfireContext, string, string, string>;
    using JobParameterExistsFunc = Func<HangfireContext, long, string, bool>;

    internal class EFCoreStorageConnection : JobStorageConnection
    {
        private static GetAllEntriesFromHashFunc GetAllEntriesFromHashFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                from x in context.Set<HangfireHash>()
                where x.Key == key
                select new KeyValuePair<string, string>(x.Field, x.Value));

        private static GetAllItemsFromListFunc GetAllItemsFromListFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                from x in context.Set<HangfireList>()
                where x.Key == key
                orderby x.Position descending
                select x.Value);

        private static GetAllItemsFromSetFunc GetAllItemsFromSetFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                from x in context.Set<HangfireSet>()
                where x.Key == key
                select x.Value);

        private static GetCounterFunc GetCounterFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                context.Set<HangfireCounter>().
                Where(x => x.Key == key).
                Sum(x => x.Value));

        private static GetFirstByLowestScoreFromSetFunc GetFirstByLowestScoreFromSetFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key, double from, double to) => (
                from x in context.Set<HangfireSet>()
                where x.Key == key && @from <= x.Score && x.Score <= to
                orderby x.Score
                select x.Value).
                FirstOrDefault());

        private static GetHashCountFunc GetHashCountFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                context.Set<HangfireHash>().LongCount(x => x.Key == key));

        private static GetHashFieldsFunc GetHashFieldsFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                from x in context.Set<HangfireHash>()
                where x.Key == key
                select x.Field);

        private static GetHashTtlFunc GetHashTtlFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) => (
                from x in context.Set<HangfireHash>()
                where x.Key == key
                select x.ExpireAt).
                Min());

        private static GetJobDataFunc GetJobDataFunc { get; } = EF.CompileQuery(
            (HangfireContext context, long id) => (
                from x in context.Set<HangfireJob>()
                where x.Id == id
                select CreateJobData(x.InvocationData, x.CreatedAt, x.State.Name)).
                FirstOrDefault());

        private static GetJobParameterFunc GetJobParameterFunc { get; } = EF.CompileQuery(
            (HangfireContext context, long id, string name) => (
                from x in context.Set<HangfireJobParameter>()
                where x.JobId == id && x.Name == name
                select x.Value).
                SingleOrDefault());

        private static GetListCountFunc GetListCountFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                context.Set<HangfireList>().LongCount(x => x.Key == key));

        private static GetListTtlFunc GetListTtlFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) => (
                from x in context.Set<HangfireList>()
                where x.Key == key
                select x.ExpireAt).
                Min());

        private static GetRangeFromListFunc GetRangeFromListFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key, int from, int to) =>
                from x in context.Set<HangfireList>()
                where x.Key == key
                let position = x.Position
                where @from <= position && position <= to
                orderby position descending
                select x.Value);

        private static GetRangeFromSetFunc GetRangeFromSetFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key, int skip, int take) => (
                from x in context.Set<HangfireSet>()
                where x.Key == key
                orderby x.Score
                select x.Value).
                Skip(skip).
                Take(take));

        private static GetSetCountFunc GetSetCountFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) =>
                context.Set<HangfireSet>().LongCount(x => x.Key == key));

        private static GetSetTtlFunc GetSetTtlFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key) => (
                from x in context.Set<HangfireSet>()
                where x.Key == key
                select x.ExpireAt).
                Min());

        private static GetStateDataFunc GetStateDataFunc { get; } = EF.CompileQuery(
            (HangfireContext context, long id) => (
                from x in context.Set<HangfireJob>()
                where x.Id == id
                let s = x.State
                select new StateData
                {
                    Name = s.Name,
                    Reason = s.Reason,
                    Data = s.Data,
                }).
                SingleOrDefault());

        private static GetTimedOutServersFunc GetTimedOutServersFunc { get; } = EF.CompileQuery(
            (HangfireContext context, DateTime outdate) =>
                from x in context.Set<HangfireServer>()
                where x.Heartbeat <= outdate
                select x.Id);

        private static GetValueFromHashFunc GetValueFromHashFunc { get; } = EF.CompileQuery(
            (HangfireContext context, string key, string name) => (
                from hash in context.Set<HangfireHash>()
                where hash.Key == key && hash.Field == name
                select hash.Value).
                SingleOrDefault());

        private static JobParameterExistsFunc JobParameterExistsFunc { get; } = EF.CompileQuery(
            (HangfireContext context, long id, string name) =>
                context.Set<HangfireJobParameter>().Any(x => x.JobId == id && x.Name == name));

        private readonly ILockProvider _lockProvider;
        private readonly EFCoreStorage _storage;

        public EFCoreStorageConnection(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _lockProvider = new EFCoreLockProvider(_storage);
        }

        public override IDisposable AcquireDistributedLock(
            [NotNull] string resource,
            TimeSpan timeout)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
            if (resource.Length == 0)
                throw new ArgumentException(CoreStrings.ArgumentExceptionStringCannotBeEmpty, nameof(resource));
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                    CoreStrings.ArgumentOutOfRangeExceptionNeedNonNegativeValue);

            return new EFCoreLock(_lockProvider, resource, timeout);
        }

        public override void AnnounceServer(
            [NotNull] string serverId,
            [NotNull] ServerContext context)
        {
            if (serverId == null)
                throw new ArgumentNullException(nameof(serverId));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var timestamp = DateTime.UtcNow;

            _storage.UseContextSavingChanges(dbContext =>
            {
                var server = new HangfireServer
                {
                    Id = serverId,
                    StartedAt = timestamp,
                    Heartbeat = timestamp,
                    WorkerCount = context.WorkerCount,
                    Queues = context.Queues,
                };

                if (!dbContext.Set<HangfireServer>().Any(x => x.Id == serverId))
                    dbContext.Add(server);
                else
                    dbContext.Entry(server).State = EntityState.Modified;
            });
        }

        public override string CreateExpiredJob(
            [NotNull] Job job,
            [NotNull] IDictionary<string, string> parameters,
            DateTime createdAt,
            TimeSpan expireIn)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var invocationData = InvocationData.Serialize(job);

            var hangfireJob = new HangfireJob
            {
                CreatedAt = createdAt,
                ExpireAt = createdAt + expireIn,
                InvocationData = invocationData,
                Parameters = parameters.
                    Select(x => new HangfireJobParameter
                    {
                        Name = x.Key,
                        Value = x.Value,
                    }).
                    ToList(),
            };

            return _storage.UseContext(context =>
            {
                context.Add(hangfireJob);
                context.SaveChanges();
                return hangfireJob.Id.ToString(CultureInfo.InvariantCulture);
            });
        }

        public override IWriteOnlyTransaction CreateWriteTransaction()
        {
            return new EFCoreStorageTransaction(
                _storage);
        }

        public override IFetchedJob FetchNextJob(
            [NotNull] string[] queues,
            CancellationToken cancellationToken)
        {
            if (queues == null)
                throw new ArgumentNullException(nameof(queues));
            if (queues.Length == 0)
                throw new ArgumentException(CoreStrings.ArgumentExceptionCollectionCannotBeEmpty,
                    nameof(queues));

            var provider = new EFCoreJobQueueProvider(_storage);
            var queue = provider.GetJobQueue();
            return queue.Dequeue(queues, cancellationToken);
        }

        public override Dictionary<string, string> GetAllEntriesFromHash([NotNull] string key)
        {
            var result = UseContext(GetAllEntriesFromHashFunc, key).
                ToDictionary(x => x.Key, x => x.Value);

            return result.Count != 0 ? result : null;
        }

        public override List<string> GetAllItemsFromList([NotNull] string key) =>
            UseContext(GetAllItemsFromListFunc, key).ToList();

        public override HashSet<string> GetAllItemsFromSet([NotNull] string key) =>
            new HashSet<string>(UseContext(GetAllItemsFromSetFunc, key));

        public override long GetCounter([NotNull] string key) => UseContext(GetCounterFunc, key);

        public override string GetFirstByLowestScoreFromSet(
            [NotNull] string key,
            double fromScore,
            double toScore) =>
            UseContext((context, k) =>
            {
                if (toScore < fromScore)
                    Swap(ref fromScore, ref toScore);

                return GetFirstByLowestScoreFromSetFunc(context, k, fromScore, toScore);
            }, key);

        public override long GetHashCount([NotNull] string key) =>
            UseContext(GetHashCountFunc, key);

        public override TimeSpan GetHashTtl([NotNull] string key) =>
            ToTtl(UseContext(GetHashTtlFunc, key));

        public override JobData GetJobData([NotNull] string jobId)
        {
            if (jobId == null)
                throw new ArgumentNullException(nameof(jobId));

            if (!TryParseJobId(jobId, out var id))
                return null;

            return _storage.UseContext(context => GetJobDataFunc(context, id));
        }

        public override string GetJobParameter([NotNull] string id, [NotNull] string name)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (!TryParseJobId(id, out var jobId))
                return null;

            return _storage.UseContext(context => GetJobParameterFunc(context, jobId, name));
        }

        public override long GetListCount([NotNull] string key) =>
            UseContext(GetListCountFunc, key);

        public override TimeSpan GetListTtl([NotNull] string key) =>
            ToTtl(UseContext(GetListTtlFunc, key));

        public override List<string> GetRangeFromList(
            [NotNull] string key,
            int startingFrom,
            int endingAt) =>
            UseContext((context, k) =>
            {
                if (endingAt < startingFrom)
                    Swap(ref startingFrom, ref endingAt);

                return GetRangeFromListFunc(context, k, startingFrom, endingAt).ToList();
            }, key);

        public override List<string> GetRangeFromSet(
            [NotNull] string key,
            int startingFrom,
            int endingAt) =>
            UseContext((context, k) =>
            {
                if (endingAt < startingFrom)
                    Swap(ref startingFrom, ref endingAt);
                int take = endingAt - startingFrom + 1;

                return GetRangeFromSetFunc(context, key, startingFrom, take).ToList();
            }, key);
        

        public override long GetSetCount([NotNull] string key) =>
            UseContext(GetSetCountFunc, key);

        public override TimeSpan GetSetTtl([NotNull] string key) =>
            ToTtl(UseContext(GetSetTtlFunc, key));

        public override StateData GetStateData([NotNull] string jobId)
        {
            if (jobId == null)
                throw new ArgumentNullException(nameof(jobId));

            if (!TryParseJobId(jobId, out var id))
                return null;

            return _storage.UseContext(context => GetStateDataFunc(context, id));
        }

        public override string GetValueFromHash([NotNull] string key, [NotNull] string name)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return _storage.UseContext(context => GetValueFromHashFunc(context, key, name));
        }

        public override void Heartbeat([NotNull] string serverId)
        {
            if (serverId == null)
                throw new ArgumentNullException(nameof(serverId));

            _storage.UseContext(context =>
            {
                var entry = context.Attach(new HangfireServer
                {
                    Id = serverId,
                    Heartbeat = DateTime.UtcNow,
                });
                entry.Property(x => x.Heartbeat).IsModified = true;
                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else already has deleted this record. Database wins.
                }
            });
        }

        public override void RemoveServer([NotNull] string serverId)
        {
            if (serverId == null)
                throw new ArgumentNullException(nameof(serverId));

            _storage.UseContext(context =>
            {
                context.Remove(new HangfireServer
                {
                    Id = serverId,
                });
                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Someone else already has deleted this record. Database wins.
                }
            });
        }

        public override int RemoveTimedOutServers(TimeSpan timeOut)
        {
            if (timeOut < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut,
                    CoreStrings.ArgumentOutOfRangeExceptionNeedNonNegativeValue);

            return _storage.UseContextSavingChanges(context =>
            {
                var ids = GetTimedOutServersFunc(context, DateTime.UtcNow - timeOut).ToList();
                var count = ids.Count;
                if (count == 0)
                    return 0;

                context.RemoveRange(ids.Select(x => new HangfireServer
                {
                    Id = x,
                }));

                try
                {
                    return context.SaveChanges();
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    // Someone else already has deleted this record. Database wins.
                    return count - exception.Entries.Count;
                }
            });
        }

        public override void SetJobParameter(
            [NotNull] string id,
            [NotNull] string name,
            string value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            long jobId = ValidateId(id);

            var parameter = new HangfireJobParameter
            {
                JobId = jobId,
                Name = name,
                Value = value,
            };

            _storage.UseContextSavingChanges(context =>
            {
                if (JobParameterExistsFunc(context, jobId, name))
                    context.Update(parameter);
                else
                    context.Add(parameter);
            });
        }

        public override void SetRangeInHash(
            [NotNull] string key,
            [NotNull] IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (keyValuePairs == null)
                throw new ArgumentNullException(nameof(keyValuePairs));

            var hashes = keyValuePairs.Select(x => new HangfireHash
            {
                Key = key,
                Field = x.Key,
                Value = x.Value,
            });

            _storage.UseContextSavingChanges(context =>
            {
                var fields = new HashSet<string>(GetHashFieldsFunc(context, key));

                foreach (var hash in hashes)
                    if (!fields.Contains(hash.Field))
                        context.Add(hash);
                    else
                        context.Update(hash);
            });
        }

        private T UseContext<T>(Func<HangfireContext, string, T> func, string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.UseContext(context => func(context, key));
        }

        private static JobData CreateJobData(InvocationData data, DateTime createdAt, string state)
        {
            var result = new JobData
            {
                State = state,
                CreatedAt = createdAt,
            };
            try
            {
                result.Job = data.Deserialize();
            }
            catch (JobLoadException exception)
            {
                result.LoadException = exception;
            }

            return result;
        }

        private static void Swap<T>(ref T left, ref T right)
        {
            var temp = left;
            left = right;
            right = temp;
        }

        private static TimeSpan ToTtl(DateTime? expireAt) =>
           expireAt.HasValue ?
           expireAt.Value - DateTime.UtcNow :
           new TimeSpan(0, 0, -1);

        private static bool TryParseJobId(string jobId, out long id) =>
            long.TryParse(jobId, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

        private static long ValidateId(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (id.Length == 0)
                throw new ArgumentException(CoreStrings.ArgumentExceptionStringCannotBeEmpty,
                    nameof(id));

            return long.Parse(id, CultureInfo.InvariantCulture);
        }
    }
}
