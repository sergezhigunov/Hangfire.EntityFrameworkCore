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
    public void Execute_RemovesOutdatedRecords()
    {
        CreateExpirationEntries(DateTime.UtcNow.AddMonths(-1));
        var instance = new ExpirationManager(Storage);
        var source = new CancellationTokenSource(0);

        instance.Execute(source.Token);

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
    public void Execute_DoesNotRemoveEntries_WithNoExpirationTimeSet()
    {
        CreateExpirationEntries(null);
        var instance = new ExpirationManager(Storage);
        var source = new CancellationTokenSource(0);

        instance.Execute(source.Token);

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
    public void Execute_DoesNotRemoveEntries_WithFreshExpirationTime()
    {
        CreateExpirationEntries(DateTime.UtcNow.AddMonths(1));
        var instance = new ExpirationManager(Storage);
        var source = new CancellationTokenSource(0);

        instance.Execute(source.Token);

        UseContext(context =>
        {
            Assert.Equal(1, context.Set<HangfireCounter>().Count());
            Assert.Equal(1, context.Set<HangfireJob>().Count());
            Assert.Equal(1, context.Set<HangfireList>().Count());
            Assert.Equal(1, context.Set<HangfireSet>().Count());
            Assert.Equal(1, context.Set<HangfireHash>().Count());
        });
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
