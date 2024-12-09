using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Hangfire.EntityFrameworkCore.Properties;
using Hangfire.States;

namespace Hangfire.EntityFrameworkCore;

using GetHashFieldsFunc = Func<DbContext, string, IEnumerable<string>>;
using GetSetValuesFunc = Func<DbContext, string, IEnumerable<string>>;
using GetListsFunc = Func<DbContext, string, IEnumerable<HangfireList>>;
using GetListPositionsFunc = Func<DbContext, string, IEnumerable<int>>;
using GetMaxListPositionFunc = Func<DbContext, string, int?>;
using SetExistsFunc = Func<DbContext, string, string, bool>;
using NotNullAttribute = Annotations.NotNullAttribute;

internal sealed class EFCoreStorageTransaction : JobStorageTransaction
{
    private static GetHashFieldsFunc GetHashFieldsFunc { get; } = EF.CompileQuery(
        (DbContext context, string key) =>
            from x in context.Set<HangfireHash>()
            where x.Key == key
            select x.Field);

    private static GetListPositionsFunc GetListPositionsFunc { get; } = EF.CompileQuery(
        (DbContext context, string key) =>
            from item in context.Set<HangfireList>()
            where item.Key == key
            select item.Position);

    private static GetListsFunc GetListsFunc { get; } = EF.CompileQuery(
        (DbContext context, string key) =>
            from item in context.Set<HangfireList>()
            where item.Key == key
            select item);

    private static GetMaxListPositionFunc GetMaxListPositionFunc { get; } = EF.CompileQuery(
        (DbContext context, string key) =>
            context.Set<HangfireList>().
                Where(x => x.Key == key).
                Max(x => (int?)x.Position));

    private static GetSetValuesFunc GetSetValuesFunc { get; } = EF.CompileQuery(
        (DbContext context, string key) =>
            from set in context.Set<HangfireSet>()
            where set.Key == key
            select set.Value);

    private static SetExistsFunc SetExistsFunc { get; } = EF.CompileQuery(
        (DbContext context, string key, string value) =>
            context.Set<HangfireSet>().Any(x => x.Key == key && x.Value == value));

    private readonly EFCoreStorage _storage;
    private readonly Queue<Action<DbContext>> _queue;
    private readonly Queue<Action> _afterCommitQueue;
    private bool _disposed;

    [SuppressMessage("Maintainability", "CA1510")]
    public EFCoreStorageTransaction(
        EFCoreStorage storage)
    {
        if (storage is null)
            throw new ArgumentNullException(nameof(storage));

        _storage = storage;
        _queue = new Queue<Action<DbContext>>();
        _afterCommitQueue = new Queue<Action>();
    }

    public override void AddJobState([NotNull] string jobId, [NotNull] IState state)
    {
        AddJobState(jobId, state, false);
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public override void AddRangeToSet([NotNull] string key, [NotNull] IList<string> items)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (items is null)
            throw new ArgumentNullException(nameof(items));
        ThrowIfDisposed();

        var values = new HashSet<string>(items);

        _queue.Enqueue(context =>
        {
            var entries = context
                .FindEntries<HangfireSet>(
                    x => x.Key == key && values.Contains(x.Value))
                .ToDictionary(x => x.Entity.Value);
            var exisitingValues = new HashSet<string>(GetSetValuesFunc(context, key));
            foreach (var value in values)
            {
                if (!entries.TryGetValue(value, out var entry))
                {
                    if (!exisitingValues.Contains(value))
                        context.Add(new HangfireSet
                        {
                            Key = key,
                            Value = value,
                        });
                }
                else if (exisitingValues.Contains(value))
                    entry.State = EntityState.Unchanged;
                else
                {
                    entry.Entity.Score = 0;
                    entry.State = EntityState.Added;
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
        switch (persistentQueue)
        {
            case EFCoreJobQueue storageJobQueue:
                _queue.Enqueue(context => context.Add(new HangfireQueuedJob
                {
                    JobId = id,
                    Queue = queue,
                }));
                break;
            default:
                _queue.Enqueue(context => persistentQueue.Enqueue(queue, jobId));
                break;
        }
        if (persistentQueue is EFCoreJobQueue)
            _afterCommitQueue.Enqueue(
                () => EFCoreJobQueue.NewItemInQueueEvent.Set());
    }

    public override void AddToSet([NotNull] string key, [NotNull] string value)
    {
        AddToSet(key, value, 0d);
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public override void AddToSet([NotNull] string key, [NotNull] string value, double score)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var entry = context.FindEntry<HangfireSet>(x => x.Key == key && x.Value == value);
            if (entry != null)
            {
                var entity = entry.Entity;
                entity.Score = score;
                entry.State = EntityState.Modified;
            }
            else
            {
                context.Attach(new HangfireSet
                {
                    Key = key,
                    Score = score,
                    Value = value,
                }).State =
                    SetExistsFunc(context, key, value) ?
                    EntityState.Modified :
                    EntityState.Added;
            }
        });
    }

    public override void Commit()
    {
        ThrowIfDisposed();

        _storage.UseContextSavingChanges(context =>
        {
            while (_queue.Count > 0)
            {
                var action = _queue.Dequeue();
                action.Invoke(context);
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

    [SuppressMessage("Maintainability", "CA1510")]
    public override void InsertToList([NotNull] string key, string value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var maxPosition = context.FindEntries<HangfireList>(x => x.Key == key)
                .Max(x => (int?)x.Entity.Position)
                ?? GetMaxListPositionFunc(context, key)
                ?? -1;
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

    [SuppressMessage("Maintainability", "CA1510")]
    public override void RemoveFromList([NotNull] string key, string value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var list = GetListsFunc(context, key).OrderBy(x => x.Position).ToList();
            var newList = list.Where(x => x.Value != value).ToList();
            for (int i = newList.Count; i < list.Count; i++)
                context.Remove(list[i]);
            CopyNonKeyValues(newList, list);
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public override void RemoveFromSet([NotNull] string key, [NotNull] string value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var entry = context.FindEntry<HangfireSet>(x => x.Key == key && x.Value == value);
            if (SetExistsFunc(context, key, value))
            {
                entry ??= context.Attach(new HangfireSet
                    {
                        Key = key,
                        Value = value,
                    });
                entry.State = EntityState.Deleted;
            }
            else if (entry != null && entry.State != EntityState.Detached)
                entry.State = EntityState.Detached;
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public override void RemoveHash([NotNull] string key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var entries = context.FindEntries<HangfireHash>(x => x.Key == key)
            .ToDictionary(x => x.Entity.Field);

            var fields = GetHashFieldsFunc(context, key);

            foreach (var field in fields)
                if (entries.TryGetValue(field, out var entry) &&
                    entry.State != EntityState.Deleted)
                    entry.State = EntityState.Deleted;
                else
                    context.Remove(new HangfireHash
                    {
                        Key = key,
                        Field = field,
                    });
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public override void RemoveSet([NotNull] string key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var entries = context.FindEntries<HangfireSet>(x => x.Key == key)
                .ToDictionary(x => x.Entity.Value);
            var values = GetSetValuesFunc(context, key);
            foreach (var value in values)
                if (entries.TryGetValue(value, out var entry) &&
                    entry.State != EntityState.Deleted)
                    entry.State = EntityState.Deleted;
                else
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

    [SuppressMessage("Maintainability", "CA1510")]
    public override void SetRangeInHash(
        [NotNull] string key,
        [NotNull] IEnumerable<KeyValuePair<string, string>> keyValuePairs)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (keyValuePairs is null)
            throw new ArgumentNullException(nameof(keyValuePairs));
        ThrowIfDisposed();

        var fields = new HashSet<string>(keyValuePairs.Select(x => x.Key));

        _queue.Enqueue(context =>
        {
            var exisitingFields = new HashSet<string>(GetHashFieldsFunc(context, key));
            var entries = context.FindEntries<HangfireHash>(x => x.Key == key && fields.Contains(x.Field))
                .ToDictionary(x => x.Entity.Field);

            foreach (var item in keyValuePairs)
            {
                var field = item.Key;
                if (!entries.TryGetValue(field, out var entry))
                {
                    var hash = new HangfireHash
                    {
                        Key = key,
                        Field = field,
                    };
                    if (exisitingFields.Contains(field))
                        entry = context.Attach(hash);
                    else
                        entry = context.Add(hash);
                }
                entry.Entity.Value = item.Value;
            }
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    public override void TrimList([NotNull] string key, int keepStartingFrom, int keepEndingAt)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var list = GetListsFunc(context, key)
                .OrderBy(x => x.Position)
                .ToList();
            var newList = list.
                Where((item, index) => keepStartingFrom <= index && index <= keepEndingAt).
                ToList();
            for (int i = newList.Count; i < list.Count; i++)
                context.Remove(list[i]);
            CopyNonKeyValues(newList, list);
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    private void AddCounter([NotNull] string key, long value, DateTime? expireAt)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var entity = (
                context.FindEntries<HangfireCounter>(x => x.Key == key)
                    .FirstOrDefault(x => x.State == EntityState.Added)
                    ?? context.Add(new HangfireCounter
                    {
                        Key = key,
                    }))
                .Entity;

            entity.Value += value;
            entity.ExpireAt = expireAt;
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    private void AddJobState([NotNull] string jobId, [NotNull] IState state, bool setActual)
    {
        var id = ValidateJobId(jobId);
        if (state is null)
            throw new ArgumentNullException(nameof(state));
        ThrowIfDisposed();

        var data = state.SerializeData();
        var createdAt = state.GetCreatedAt() ?? DateTime.UtcNow;

        _queue.Enqueue(context =>
        {
            var stateEntity = context
                .Add(new HangfireState
                {
                    JobId = id,
                    CreatedAt = createdAt,
                    Name = state.Name,
                    Reason = state.Reason,
                    Data = data,
                })
                .Entity;
            if (setActual)
            {
                var jobEntry = context.FindEntry<HangfireJob>(x => x.Id == id) ??
                    context.Attach(new HangfireJob
                    {
                        Id = id,
                        State = stateEntity,
                        StateName = state.Name,
                    });
                jobEntry.Property(x => x.StateName).IsModified = true;
                jobEntry.Navigation(nameof(HangfireJob.State)).IsModified = true;
            }
        });
    }

    private static void CopyNonKeyValues(
        List<HangfireList> source,
        List<HangfireList> destination)
    {
        var count = source.Count;
        for (int i = 0; i < count; i++)
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
            var entry = context.FindEntry<HangfireJob>(x => x.Id == id);
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

    [SuppressMessage("Maintainability", "CA1510")]
    private void SetHashExpiration(string key, DateTime? expireAt)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var fields = new HashSet<string>(GetHashFieldsFunc(context, key));
            var entries = context.FindEntries<HangfireHash>(x => x.Key == key && fields.Contains(x.Field))
                .ToDictionary(x => x.Entity.Field);
            foreach (var field in fields)
            {
                if (!entries.TryGetValue(field, out var entry))
                    entry = context.Attach(new HangfireHash
                    {
                        Key = key,
                        Field = field,
                    });

                entry.Entity.ExpireAt = expireAt;
                entry.Property(x => x.ExpireAt).IsModified = true;
            }
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    private void SetListExpiration(string key, DateTime? expireAt)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var positions = new HashSet<int>(GetListPositionsFunc(context, key));
            var entries = context.FindEntries<HangfireList>(x => x.Key == key && positions.Contains(x.Position))
                .ToDictionary(x => x.Entity.Position);
            foreach (var position in positions)
            {
                if (!entries.TryGetValue(position, out var entry))
                    entry = context.Attach(new HangfireList
                    {
                        Key = key,
                        Position = position,
                    });
                entry.Entity.ExpireAt = expireAt;
                entry.Property(x => x.ExpireAt).IsModified = true;
            }
        });
    }

    [SuppressMessage("Maintainability", "CA1510")]
    private void SetSetExpiration(string key, DateTime? expireAt)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        ThrowIfDisposed();

        _queue.Enqueue(context =>
        {
            var values = new HashSet<string>(GetSetValuesFunc(context, key));
            var entries = context.FindEntries<HangfireSet>(x => x.Key == key && values.Contains(x.Value))
                .ToDictionary(x => x.Entity.Value);
            foreach (var value in values)
            {
                if (!entries.TryGetValue(value, out var entry))
                    entry = context.Attach(new HangfireSet
                    {
                        Key = key,
                        Value = value,
                    });
                entry.Entity.ExpireAt = expireAt;
                entry.Property(x => x.ExpireAt).IsModified = true;
            }
        });
    }

    [SuppressMessage("Maintainability", "CA1513")]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().FullName);
    }

    [SuppressMessage("Maintainability", "CA1510")]
    private static void ValidateQueue(string queue)
    {
        if (queue is null)
            throw new ArgumentNullException(nameof(queue));
        if (queue.Length == 0)
            throw new ArgumentException(CoreStrings.ArgumentExceptionStringCannotBeEmpty,
                nameof(queue));
    }

    [SuppressMessage("Maintainability", "CA1510")]
    private static long ValidateJobId(string jobId)
    {
        if (jobId is null)
            throw new ArgumentNullException(nameof(jobId));
        if (jobId.Length == 0)
            throw new ArgumentException(CoreStrings.ArgumentExceptionStringCannotBeEmpty,
                nameof(jobId));

        return long.Parse(jobId, CultureInfo.InvariantCulture);
    }
}
