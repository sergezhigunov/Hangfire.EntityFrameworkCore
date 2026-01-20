using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore.Tests;

public class ExpirationManagerFacts : EFCoreStorageTest
{
    [Fact]
    public static void Ctor_Throws_WhenStorageParameterIsNull()
    {
        EFCoreStorage storage = null;

        Assert.Throws<ArgumentNullException>(nameof(storage),
            () => new ExpirationManager(storage));
    }

    [Fact]
    public void Ctor_CreatesInstance()
    {
        var storage = CreateStorageStub();

        var instance = new ExpirationManager(storage);

        Assert.Same(storage, Assert.IsType<EFCoreStorage>(instance.GetFieldValue("_storage")));
    }

    [Fact]
    public void Execute_Process_RemovesOutdatedRecords()
    {
        CreateExpirationEntries(DateTime.UtcNow.AddMonths(-1));
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.False(context.Set<HangfireCounter>().Any());
            Assert.False(context.Set<HangfireJob>().Any());
            Assert.False(context.Set<HangfireList>().Any());
            Assert.False(context.Set<HangfireSet>().Any());
            Assert.False(context.Set<HangfireHash>().Any());
        });
    }

    [Fact]
    public void Execute_Process_DoesNotRemoveEntries_WithNoExpirationTimeSet()
    {
        CreateExpirationEntries(null);
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.Equal(1, context.Set<HangfireCounter>().Count());
            Assert.Equal(1, context.Set<HangfireJob>().Count());
            Assert.Equal(1, context.Set<HangfireList>().Count());
            Assert.Equal(1, context.Set<HangfireSet>().Count());
            Assert.Equal(1, context.Set<HangfireHash>().Count());
        });
    }

    [Fact]
    public void Execute_Process_DoesNotRemoveEntries_WithFreshExpirationTime()
    {
        CreateExpirationEntries(DateTime.UtcNow.AddMonths(1));
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.Equal(1, context.Set<HangfireCounter>().Count());
            Assert.Equal(1, context.Set<HangfireJob>().Count());
            Assert.Equal(1, context.Set<HangfireList>().Count());
            Assert.Equal(1, context.Set<HangfireSet>().Count());
            Assert.Equal(1, context.Set<HangfireHash>().Count());
        });
    }

    [Fact]
    public void Execute__Process_RemovesOutdatedJobs_WithActualStateSet()
    {
        var now = DateTime.UtcNow;

        var job = new HangfireJob
        {
            CreatedAt = now,
            InvocationData = CreateInvocationData(() => SampleMethod("test")),
            ExpireAt = now.AddMonths(-1),
            States =
            [
                new HangfireState
                {
                    CreatedAt = now,
                    Data = EmptyDictionaryStub,
                    Name = "Created",
                    Reason = "Reason",
                },
            ],
        };
        UseContext(context =>
        {
            using var transaction = context.Database.BeginTransaction();
            context.Add(job);
            context.SaveChanges();
            var state = job.States.First();
            job.StateName = state.Name;
            job.StateId = state.Id;
            context.SaveChanges();
            transaction.Commit();
        });
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.False(context.Set<HangfireState>().Any());
            Assert.False(context.Set<HangfireJob>().Any());
        });
    }

    [Fact]
    public void Execute_Process_RemovesOutdatedJobs_WithParametersSet()
    {
        var now = DateTime.UtcNow;

        var job = new HangfireJob
        {
            CreatedAt = now,
            InvocationData = CreateInvocationData(() => SampleMethod("test")),
            ExpireAt = now.AddMonths(-1),
            Parameters =
            [
                new HangfireJobParameter
                {
                    Name = "TestName",
                    Value = "TestValue"
                }
            ],
        };
        UseContextSavingChanges(context => context.Add(job));
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.False(context.Set<HangfireJobParameter>().Any());
            Assert.False(context.Set<HangfireState>().Any());
            Assert.False(context.Set<HangfireJob>().Any());
        });
    }

    [Fact]
    public void Execute_Component_RemovesOutdatedRecords()
    {
        CreateExpirationEntries(DateTime.UtcNow.AddMonths(-1));
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.False(context.Set<HangfireCounter>().Any());
            Assert.False(context.Set<HangfireJob>().Any());
            Assert.False(context.Set<HangfireList>().Any());
            Assert.False(context.Set<HangfireSet>().Any());
            Assert.False(context.Set<HangfireHash>().Any());
        });
    }

    [Fact]
    public void Execute_Component_DoesNotRemoveEntries_WithNoExpirationTimeSet()
    {
        CreateExpirationEntries(null);
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.Equal(1, context.Set<HangfireCounter>().Count());
            Assert.Equal(1, context.Set<HangfireJob>().Count());
            Assert.Equal(1, context.Set<HangfireList>().Count());
            Assert.Equal(1, context.Set<HangfireSet>().Count());
            Assert.Equal(1, context.Set<HangfireHash>().Count());
        });
    }

    [Fact]
    public void Execute_Component_DoesNotRemoveEntries_WithFreshExpirationTime()
    {
        CreateExpirationEntries(DateTime.UtcNow.AddMonths(1));
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.Equal(1, context.Set<HangfireCounter>().Count());
            Assert.Equal(1, context.Set<HangfireJob>().Count());
            Assert.Equal(1, context.Set<HangfireList>().Count());
            Assert.Equal(1, context.Set<HangfireSet>().Count());
            Assert.Equal(1, context.Set<HangfireHash>().Count());
        });
    }

    [Fact]
    public void Execute_Component_RemovesOutdatedJobs_WithActualStateSet()
    {
        var now = DateTime.UtcNow;

        var job = new HangfireJob
        {
            CreatedAt = now,
            InvocationData = CreateInvocationData(() => SampleMethod("test")),
            ExpireAt = now.AddMonths(-1),
            States =
            [
                new HangfireState
                {
                    CreatedAt = now,
                    Data = EmptyDictionaryStub,
                    Name = "Created",
                    Reason = "Reason",
                },
            ],
        };
        UseContext(context =>
        {
            using var transaction = context.Database.BeginTransaction();
            context.Add(job);
            context.SaveChanges();
            var state = job.States.First();
            job.StateName = state.Name;
            job.StateId = state.Id;
            context.SaveChanges();
            transaction.Commit();
        });
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.False(context.Set<HangfireState>().Any());
            Assert.False(context.Set<HangfireJob>().Any());
        });
    }

    [Fact]
    public void Execute_Component_RemovesOutdatedJobs_WithParametersSet()
    {
        var now = DateTime.UtcNow;

        var job = new HangfireJob
        {
            CreatedAt = now,
            InvocationData = CreateInvocationData(() => SampleMethod("test")),
            ExpireAt = now.AddMonths(-1),
            Parameters =
            [
                new HangfireJobParameter
                {
                    Name = "TestName",
                    Value = "TestValue"
                }
            ],
        };
        UseContextSavingChanges(context => context.Add(job));
        (var instance, var context) = CreateInitialState();

        instance.Execute(context);

        UseContext(context =>
        {
            Assert.False(context.Set<HangfireJobParameter>().Any());
            Assert.False(context.Set<HangfireState>().Any());
            Assert.False(context.Set<HangfireJob>().Any());
        });
    }
    private (ExpirationManager instance, BackgroundProcessContext context) CreateInitialState()
    {
        var instance = new ExpirationManager(Storage);
        var source = new CancellationTokenSource();
        source.Cancel();
        var context = new BackgroundProcessContext(
            "1", Storage, new Dictionary<string, object>(), default, source.Token, default, default);
        return (instance, context);
    }

    private void CreateExpirationEntries(DateTime? expireAt)
    {
        var now = DateTime.UtcNow;

        UseContextSavingChanges(context =>
        {
            context.Add(new HangfireCounter
            {
                Key = "test",
                ExpireAt = expireAt,
            });

            context.Add(new HangfireJob
            {
                CreatedAt = now,
                InvocationData = CreateInvocationData(() => SampleMethod("test")),
                ExpireAt = expireAt,
            });

            context.Add(new HangfireList
            {
                Key = "test",
                ExpireAt = expireAt,
            });

            context.Add(new HangfireSet
            {
                Key = "test",
                Value = "test",
                ExpireAt = expireAt,
            });

            context.Add(new HangfireHash
            {
                Key = "test",
                Field = "test",
                ExpireAt = expireAt,
            });
        });
    }
}
