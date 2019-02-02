using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class EFCoreStorageConnection : JobStorageConnection
    {
        private readonly IDistributedLockProvider _lockProvider;
        private readonly EFCoreStorage _storage;

        public EFCoreStorageConnection(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _lockProvider = new EFCoreLockProvider(_storage, new TimeSpan(0, 10, 0));
        }

        public override IDisposable AcquireDistributedLock(
            [NotNull] string resource,
            TimeSpan timeout)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
            if (resource.Length == 0)
                throw new ArgumentException(null, nameof(resource));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(resource), timeout, null);

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

                var servers = dbContext.Servers;

                if (!servers.Any(x => x.Id == serverId))
                    servers.Add(server);
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
                context.Jobs.Add(hangfireJob);
                context.SaveChanges();
                return hangfireJob.Id.ToString(CultureInfo.InvariantCulture);
            });
        }

        public override IWriteOnlyTransaction CreateWriteTransaction()
        {
            return new EFCoreStorageTransaction(
                _storage,
                new EFCoreJobQueueProvider(_storage));
        }

        public override IFetchedJob FetchNextJob(
            [NotNull] string[] queues,
            CancellationToken cancellationToken)
        {
            if (queues == null)
                throw new ArgumentNullException(nameof(queues));
            if (queues.Length == 0)
                throw new ArgumentException(null, nameof(queues));

            var provider = new EFCoreJobQueueProvider(_storage);
            var queue = provider.GetJobQueue();
            return queue.Dequeue(queues, cancellationToken);
        }

        public override Dictionary<string, string> GetAllEntriesFromHash([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var result = _storage.UseContext(context => (
                from hash in context.Hashes
                where hash.Key == key
                select new
                {
                    hash.Field,
                    hash.Value,
                }).
                ToDictionary(x => x.Field, x => x.Value)
            );

            return result.Count != 0 ? result : null;
        }

        public override List<string> GetAllItemsFromList([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.UseContext(context => (
                from item in context.Lists
                where item.Key == key
                orderby item.Position descending
                select item.Value).
                ToList());
        }

        public override HashSet<string> GetAllItemsFromSet([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.UseContext(context => new HashSet<string>(
                from set in context.Sets
                where set.Key == key
                select set.Value));
        }

        public override long GetCounter([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.UseContext(context => { return (
                from counter in context.Counters
                where counter.Key == key
                select (long?)counter.Value).
                Sum(); }) ?? 0L;
        }

        public override string GetFirstByLowestScoreFromSet(
            [NotNull] string key,
            double fromScore,
            double toScore)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            decimal
                fromScoreValue = (decimal)fromScore,
                toScoreValue = (decimal)toScore;

            if (toScoreValue < fromScoreValue)
                Swap(ref fromScoreValue, ref toScoreValue);

            return _storage.UseContext(context => (
                from set in context.Sets
                where set.Key == key && fromScoreValue <= set.Score && set.Score <= toScoreValue
                orderby set.Score
                select set.Value).
                FirstOrDefault());
        }

        public override long GetHashCount([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.UseContext(context => context.Hashes.LongCount(x => x.Key == key));
        }

        public override TimeSpan GetHashTtl([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            DateTime? minExpiredAt = _storage.UseContext(context => (
                from hash in context.Hashes
                where hash.Key == key
                select hash.ExpireAt).
                Min());

            return minExpiredAt.HasValue ?
                minExpiredAt.Value - DateTime.UtcNow :
                new TimeSpan(0, 0, -1);
        }

        public override JobData GetJobData([NotNull] string jobId)
        {
            if (jobId == null)
                throw new ArgumentNullException(nameof(jobId));

            if (!TryParseJobId(jobId, out var id))
                return null;

            var jobInfo = _storage.UseContext(context => (
                from job in context.Jobs
                where job.Id == id
                select new
                {
                    job.InvocationData,
                    job.CreatedAt,
                    State = job.ActualState.Name,
                }).
                FirstOrDefault());

            if (jobInfo == null)
                return null;

            var jobData = new JobData
            {
                State = jobInfo.State,
                CreatedAt = jobInfo.CreatedAt,
            };

            try
            {
                jobData.Job = jobInfo.InvocationData.Deserialize();
            }
            catch (JobLoadException exception)
            {
                jobData.LoadException = exception;
            }

            return jobData;
        }

        public override string GetJobParameter([NotNull] string id, [NotNull] string name)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (!TryParseJobId(id, out var jobId))
                return null;

            return _storage.UseContext(context => (
                from parameter in context.JobParameters
                where parameter.JobId == jobId && parameter.Name == name
                select parameter.Value).
                SingleOrDefault());
        }

        public override long GetListCount([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.UseContext(context => context.Lists.LongCount(x => x.Key == key));
        }

        public override TimeSpan GetListTtl([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            DateTime? minExpiredAt = _storage.UseContext(context => (
                from set in context.Lists
                where set.Key == key
                select set.ExpireAt).
                Min());

            return minExpiredAt.HasValue ?
                minExpiredAt.Value - DateTime.UtcNow :
                TimeSpan.FromSeconds(-1);
        }

        public override List<string> GetRangeFromList(
            [NotNull] string key,
            int startingFrom,
            int endingAt)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (endingAt < startingFrom)
                Swap(ref startingFrom, ref endingAt);

            return _storage.UseContext(context => (
                from item in context.Lists
                where item.Key == key
                let position = item.Position
                where startingFrom <= position && position <= endingAt
                orderby item.Position descending
                select item.Value).
                ToList());
        }

        public override List<string> GetRangeFromSet(
            [NotNull] string key,
            int startingFrom,
            int endingAt)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (endingAt < startingFrom)
                Swap(ref startingFrom, ref endingAt);
            int take = endingAt - startingFrom + 1;

            return _storage.UseContext(context => (
                from item in context.Sets
                where item.Key == key
                orderby item.CreatedAt
                select item.Value).
                Skip(startingFrom).
                Take(take).
                ToList());
        }

        public override long GetSetCount([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storage.UseContext(context => context.Sets.LongCount(x => x.Key == key));
        }

        public override TimeSpan GetSetTtl([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            DateTime? minExpiredAt = _storage.UseContext(context => (
                from set in context.Sets
                where set.Key == key
                select set.ExpireAt).
                Min());

            return minExpiredAt.HasValue ?
                minExpiredAt.Value - DateTime.UtcNow :
                TimeSpan.FromSeconds(-1);
        }

        public override StateData GetStateData([NotNull] string jobId)
        {
            if (jobId == null)
                throw new ArgumentNullException(nameof(jobId));

            if (!TryParseJobId(jobId, out var id))
                return null;

            return _storage.UseContext(context => (
                from job in context.Jobs
                where job.Id == id
                let actualState = job.ActualState
                where actualState != null
                let state = actualState.State
                select new StateData
                {
                    Name = state.Name,
                    Reason = state.Reason,
                    Data = (Dictionary<string, string>)state.Data,
                }).
                SingleOrDefault());
        }

        public override string GetValueFromHash([NotNull] string key, [NotNull] string name)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return _storage.UseContext(context => (
                from hash in context.Hashes
                where hash.Key == key && hash.Field == name
                select hash.Value).
                SingleOrDefault());
        }

        public override void Heartbeat([NotNull] string serverId)
        {
            if (serverId == null)
                throw new ArgumentNullException(nameof(serverId));

            _storage.UseContext(context =>
            {
                var server = context.Servers.SingleOrDefault(x => x.Id == serverId);
                if (server != null)
                {
                    server.Heartbeat = DateTime.UtcNow;
                    context.SaveChanges();
                }
            });
        }

        public override void RemoveServer([NotNull] string serverId)
        {
            if (serverId == null)
                throw new ArgumentNullException(nameof(serverId));

            RemoveServers(x => x.Id == serverId);
        }

        public override int RemoveTimedOutServers(TimeSpan timeOut)
        {
            if (timeOut < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut, null);

            var outdate = DateTime.UtcNow - timeOut;
            return RemoveServers(x => x.Heartbeat <= outdate);
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
                if (!context.JobParameters.Any(x => x.JobId == jobId && x.Name == name))
                    context.Add(parameter);
                else
                    context.Entry(parameter).State = EntityState.Modified;
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
                var fields = new HashSet<string>(
                    from hash in context.Hashes
                    where hash.Key == key
                    select hash.Field);

                foreach (var hash in hashes)
                    if (!fields.Contains(hash.Field))
                        context.Add(hash);
                    else
                        context.Entry(hash).State = EntityState.Modified;
            });
        }

        private int RemoveServers(Expression<Func<HangfireServer, bool>> predicate)
        {
            return _storage.UseContextSavingChanges(context =>
            {
                var serverIds = (
                    from server in context.Servers.Where(predicate)
                    select server.Id).
                    ToArray();

                foreach (var serverId in serverIds)
                    context.Entry(new HangfireServer
                    {
                        Id = serverId,
                    }).
                    State = EntityState.Deleted;

                return serverIds.Length;
            });
        }

        private static void Swap<T>(ref T left, ref T right)
        {
            var temp = left;
            left = right;
            right = temp;
        }

        private static bool TryParseJobId(string jobId, out long id) =>
            long.TryParse(jobId, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

        private static long ValidateId(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (id.Length == 0)
                throw new ArgumentException(null, nameof(id));

            return long.Parse(id, CultureInfo.InvariantCulture);
        }
    }
}
