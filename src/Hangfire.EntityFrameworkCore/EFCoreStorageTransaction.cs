using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire.Annotations;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal sealed class EFCoreStorageTransaction : JobStorageTransaction
    {
        private readonly EFCoreStorage _storage;
        private readonly Queue<Action<HangfireContext>> _queue;
        private readonly Queue<Action> _afterCommitQueue;
        private bool _disposed;

        public EFCoreStorageTransaction(
            EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _queue = new Queue<Action<HangfireContext>>();
            _afterCommitQueue = new Queue<Action>();
        }

        public override void AddJobState([NotNull] string jobId, [NotNull] IState state)
        {
            AddJobState(jobId, state, false);
        }

        public override void AddRangeToSet([NotNull] string key, [NotNull] IList<string> items)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var exisitingFields = new HashSet<string>(
                    from set in context.Set<HangfireSet>()
                    where set.Key == key
                    select set.Value);

                foreach (var item in items)
                {
                    var set = new HangfireSet
                    {
                        Key = key,
                        Value = item,
                    };

                    if (!exisitingFields.Contains(item))
                        context.Add(set);
                    else
                    {
                        context.Attach(set).
                            Property(x => x.Score).
                            IsModified = true;
                    }
                }
            });
        }

        public override void AddToQueue([NotNull] string queue, [NotNull] string jobId)
        {
            ValidateQueue(queue);
            long id = ValidateJobId(jobId);
            ThrowIfDisposed();

            var provider = _storage.GetQueueProvider(queue);
            var persistentQueue = provider.GetJobQueue();
            if (persistentQueue is EFCoreJobQueue storageJobQueue)
                _queue.Enqueue(context => storageJobQueue.Enqueue(context, queue, id));
            else
                _queue.Enqueue(context => persistentQueue.Enqueue(queue, jobId));
            if (persistentQueue is EFCoreJobQueue)
                _afterCommitQueue.Enqueue(
                    () => EFCoreJobQueue.NewItemInQueueEvent.Set());
        }

        public override void AddToSet([NotNull] string key, [NotNull] string value)
        {
            AddToSet(key, value, 0d);
        }

        public override void AddToSet([NotNull] string key, [NotNull] string value, double score)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var entry = context.ChangeTracker.
                    Entries<HangfireSet>().
                    FirstOrDefault(x =>
                        x.Entity.Key == key &&
                        x.Entity.Value == value);

                decimal scoreValue = (decimal)score;

                if (entry != null)
                {
                    var entity = entry.Entity;
                    entity.Score = scoreValue;
                    entity.CreatedAt = DateTime.UtcNow;
                    entry.State = EntityState.Modified;
                }
                else
                {
                    var set = new HangfireSet
                    {
                        CreatedAt = DateTime.UtcNow,
                        Key = key,
                        Score = scoreValue,
                        Value = value,
                    };

                    if (!context.Set<HangfireSet>().Any(x => x.Key == key && x.Value == value))
                        context.Add(set);
                    else
                        context.Entry(set).State = EntityState.Modified;
                }
            });
        }

        public override void Commit()
        {
            ThrowIfDisposed();

            _storage.UseContext(context =>
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    while (_queue.Count > 0)
                    {
                        var action = _queue.Dequeue();
                        action.Invoke(context);
                        context.SaveChanges();
                    }
                    transaction.Commit();
                }
            });


            while (_afterCommitQueue.Count > 0)
            {
                var action = _afterCommitQueue.Dequeue();
                action.Invoke();
            }
        }

        public override void DecrementCounter([NotNull] string key)
        {
            AddCounter(key, -1L, default);
        }

        public override void DecrementCounter([NotNull] string key, TimeSpan expireIn)
        {
            AddCounter(key, -1L, DateTime.UtcNow + expireIn);
        }

        public override void Dispose()
        {
            base.Dispose();
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _queue.Clear();
                    _afterCommitQueue.Clear();
                }
                _disposed = true;
            }
        }

        public override void ExpireJob([NotNull] string jobId, TimeSpan expireIn)
        {
            SetJobExpiration(jobId, DateTime.UtcNow + expireIn);
        }

        public override void ExpireHash([NotNull] string key, TimeSpan expireIn)
        {
            SetHashExpiration(key, DateTime.UtcNow + expireIn);
        }

        public override void ExpireList([NotNull] string key, TimeSpan expireIn)
        {
            SetListExpiration(key, DateTime.UtcNow + expireIn);
        }

        public override void ExpireSet([NotNull] string key, TimeSpan expireIn)
        {
            SetSetExpiration(key, DateTime.UtcNow + expireIn);
        }

        public override void IncrementCounter([NotNull] string key)
        {
            AddCounter(key, 1L, default);
        }

        public override void IncrementCounter([NotNull] string key, TimeSpan expireIn)
        {
            AddCounter(key, 1L, DateTime.UtcNow + expireIn);
        }

        public override void InsertToList([NotNull] string key, string value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var maxPosition = context.ChangeTracker.
                    Entries<HangfireList>().
                    Where(x => x.Entity.Key == key).
                    Max(x => (int?)x.Entity.Position) ??
                    context.Set<HangfireList>().
                    Where(x => x.Key == key).
                    Max(x => (int?)x.Position) ?? -1;

                context.Add(new HangfireList
                {
                    Key = key,
                    Position = maxPosition + 1,
                    Value = value,
                });
            });
        }

        public override void PersistJob([NotNull] string jobId)
        {
            SetJobExpiration(jobId, null);
        }

        public override void PersistHash([NotNull] string key)
        {
            SetHashExpiration(key, null);
        }

        public override void PersistList([NotNull] string key)
        {
            SetListExpiration(key, null);
        }

        public override void PersistSet([NotNull] string key)
        {
            SetSetExpiration(key, null);
        }

        public override void RemoveFromList([NotNull] string key, string value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var list = (
                    from item in context.Set<HangfireList>()
                    where item.Key == key
                    orderby item.Position
                    select item).
                    ToArray();

                var newList = list.
                    Where(x => x.Value != value).
                    ToArray();

                for (int i = newList.Length; i < list.Length; i++)
                    context.Remove(list[i]);

                CopyNonKeyValues(newList, list);
            });
        }

        public override void RemoveFromSet([NotNull] string key, [NotNull] string value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var entries = context.ChangeTracker.
                    Entries<HangfireSet>().
                    Where(x => x.Entity.Key == key && x.Entity.Value == value);

                foreach (var entry in entries)
                    entry.State = EntityState.Detached;

                if (context.Set<HangfireSet>().Any(x => x.Key == key && x.Value == value))
                    context.Remove(new HangfireSet
                    {
                        Key = key,
                        Value = value
                    });
            });
        }

        public override void RemoveHash([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var fields = 
                    from hash in context.Set<HangfireHash>()
                    where hash.Key == key
                    select hash.Field;

                foreach (var field in fields)
                    context.Remove(new HangfireHash
                    {
                        Key = key,
                        Field = field,
                    });
            });
        }

        public override void RemoveSet([NotNull] string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var values = 
                    from set in context.Set<HangfireSet>()
                    where set.Key == key
                    select set.Value;

                foreach (var value in values)
                    context.Remove(new HangfireSet
                    {
                        Key = key,
                        Value = value,
                    });
            });
        }

        public override void SetJobState([NotNull] string jobId, [NotNull] IState state)
        {
            AddJobState(jobId, state, true);
        }

        public override void SetRangeInHash(
            [NotNull] string key,
            [NotNull] IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (keyValuePairs == null)
                throw new ArgumentNullException(nameof(keyValuePairs));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var exisitingFields =
                    from hash in context.Set<HangfireHash>()
                    where hash.Key == key
                    select hash.Field;

                foreach (var item in keyValuePairs)
                {
                    var hash = new HangfireHash
                    {
                        Key = key,
                        Field = item.Key,
                        Value = item.Value,
                    };

                    if (exisitingFields.Contains(item.Key))
                    {
                        context.Attach(hash).
                            Property(x => x.Value).
                            IsModified = true;
                    }
                    else
                    {
                        context.Add(hash);
                    }
                }
            });
        }

        public override void TrimList([NotNull] string key, int keepStartingFrom, int keepEndingAt)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var list = (
                    from item in context.Set<HangfireList>()
                    where item.Key == key
                    orderby item.Position
                    select item).
                    ToArray();

                var newList = list.
                    Where((item, index) => keepStartingFrom <= index && index <= keepEndingAt).
                    ToArray();

                for (int i = newList.Length; i < list.Length; i++)
                    context.Remove(list[i]);

                CopyNonKeyValues(newList, list);
            });
        }

        private void AddCounter([NotNull] string key, long value, DateTime? expireAt)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                context.Add(new HangfireCounter
                {
                    Key = key,
                    Value = value,
                    ExpireAt = expireAt,
                });
            });
        }

        private void AddJobState([NotNull] string jobId, [NotNull] IState state, bool setActual)
        {
            var id = ValidateJobId(jobId);
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var stateEntity = context.Add(new HangfireState
                {
                    JobId = id,
                    CreatedAt = DateTime.UtcNow,
                    Name = state.Name,
                    Reason = state.Reason,
                    Data = state.SerializeData(),
                }).Entity;

                if (setActual)
                {
                    var actualState = context.Set<HangfireJobState>().
                                SingleOrDefault(x => x.JobId == id);

                    if (actualState == null)
                        actualState = context.Add(new HangfireJobState
                        {
                            JobId = id,
                        }).Entity;

                    actualState.State = stateEntity;
                    actualState.Name = state.Name;
                }
            });
        }

        private static void CopyNonKeyValues(HangfireList[] source, HangfireList[] destination)
        {
            for (int i = 0; i < source.Length; i++)
            {
                var oldItem = destination[i];
                var newItem = source[i];
                if (ReferenceEquals(oldItem, newItem))
                    continue;
                oldItem.ExpireAt = newItem.ExpireAt;
                oldItem.Value = newItem.Value;
            }
        }

        private void SetJobExpiration(string jobId, DateTime? expireAt)
        {
            var id = ValidateJobId(jobId);
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var entry = context.ChangeTracker.
                    Entries<HangfireJob>().
                    FirstOrDefault(x => x.Entity.Id == id);

                if (entry != null)
                    entry.Entity.ExpireAt = expireAt;
                else
                {
                    entry = context.Attach(new HangfireJob
                    {
                        Id = id,
                        ExpireAt = expireAt,
                    });
                }

                entry.Property(x => x.ExpireAt).IsModified = true;
            });
        }

        private void SetHashExpiration(string key, DateTime? expireAt)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var fields = 
                    from hash in context.Set<HangfireHash>()
                    where hash.Key == key
                    select hash.Field;

                foreach (var field in fields)
                {
                    var hash = new HangfireHash
                    {
                        Key = key,
                        Field = field,
                        ExpireAt = expireAt,
                    };

                    context.Attach(hash).
                        Property(x => x.ExpireAt).
                        IsModified = true;
                }
            });
        }

        private void SetListExpiration(string key, DateTime? expireAt)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var ids = (
                    from item in context.Set<HangfireList>()
                    where item.Key == key
                    select new
                    {
                        item.Key,
                        item.Position,
                    }).
                    ToArray();

                foreach (var id in ids)
                {
                    var item = new HangfireList
                    {
                        Key = id.Key,
                        Position = id.Position,
                        ExpireAt = expireAt
                    };

                    context.Attach(item).
                        Property(x => x.ExpireAt).
                        IsModified = true;
                }
            });
        }

        private void SetSetExpiration(string key, DateTime? expireAt)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            ThrowIfDisposed();

            _queue.Enqueue(context =>
            {
                var ids = (
                    from item in context.Set<HangfireSet>()
                    where item.Key == key
                    select new
                    {
                        item.Key,
                        item.Value,
                    }).
                    ToArray();

                foreach (var id in ids)
                {
                    var item = new HangfireSet
                    {
                        Key = id.Key,
                        Value = id.Value,
                        ExpireAt = expireAt,
                    };

                    context.Attach(item).
                        Property(x => x.ExpireAt).
                        IsModified = true;
                }
            });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private static void ValidateQueue(string queue)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));

            if (queue.Length == 0)
                throw new ArgumentException(null, nameof(queue));
        }

        private static long ValidateJobId(string jobId)
        {
            if (jobId == null)
                throw new ArgumentNullException(nameof(jobId));

            if (jobId.Length == 0)
                throw new ArgumentException(null, nameof(jobId));

            return long.Parse(jobId, CultureInfo.InvariantCulture);
        }
    }
}
