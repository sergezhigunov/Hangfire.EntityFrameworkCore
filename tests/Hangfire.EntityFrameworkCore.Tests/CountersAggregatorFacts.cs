using Hangfire.Server;

namespace Hangfire.EntityFrameworkCore.Tests;

public class CountersAggregatorFacts : EFCoreStorageTest
{
    [Fact]
    public static void Ctor_Throws_WhenStorageParameterIsNull()
    {
        EFCoreStorage storage = null;

        Assert.Throws<ArgumentNullException>(nameof(storage),
            () => new CountersAggregator(storage));
    }

    [Fact]
    public void Ctor_CreatesInstance()
    {
        var storage = CreateStorageStub();

        var instance = new CountersAggregator(storage);

        Assert.Same(storage, Assert.IsType<EFCoreStorage>(instance.GetFieldValue("_storage")));
    }

    [Fact]
    public void Execute_Component_Throws_WhenCancellationTokenRequested()
    {
        (var instance, var context) = SetupInitialState(true);

        Assert.Throws<OperationCanceledException>(
            () => instance.Execute(context));
    }

    [Fact]
    public void Execute_Component_DoWorkCorrectly()
    {
        InitializeData();
        (var instance, var context) = SetupInitialState(false);

        instance.Execute(context);

        UseContext(context =>
        {
            var result = context.Set<HangfireCounter>().ToArray();

            Assert.Equal(3, result.Length);
            Assert.Equal(10, result.Single(x => x.Key == "counter1").Value);
            Assert.Equal(-20, result.Single(x => x.Key == "counter2").Value);
            Assert.Equal(99, result.Single(x => x.Key == "counter3").Value);
        });
    }

    [Fact]
    public void Execute_Process_Throws_WhenCancellationTokenRequested()
    {
        (var instance, var context) = SetupInitialState(true);

        Assert.Throws<OperationCanceledException>(
            () => instance.Execute(context));
    }

    [Fact]
    public void Execute_Process_DoWorkCorrectly()
    {
        InitializeData();
        (var instance, var context) = SetupInitialState(false);

        instance.Execute(context);

        UseContext(context =>
        {
            var result = context.Set<HangfireCounter>().ToArray();

            Assert.Equal(3, result.Length);
            Assert.Equal(10, result.Single(x => x.Key == "counter1").Value);
            Assert.Equal(-20, result.Single(x => x.Key == "counter2").Value);
            Assert.Equal(99, result.Single(x => x.Key == "counter3").Value);
        });
    }

    private void InitializeData()
    {
        UseContextSavingChanges(context =>
        {
            for (int i = 0; i < 10; i++)
                context.Add(new HangfireCounter
                {
                    Key = "counter1",
                    Value = 1
                });

            for (int i = 0; i < 20; i++)
                context.Add(new HangfireCounter
                {
                    Key = "counter2",
                    Value = -1
                });

            for (int i = 0; i < 5; i++)
                context.Add(new HangfireCounter
                {
                    Key = "counter3",
                    Value = 20
                });

            context.Add(new HangfireCounter
            {
                Key = "counter3",
                Value = -1
            });
        });
    }

    private (CountersAggregator instance, BackgroundProcessContext context) SetupInitialState(bool cancelled)
    {
        var storage = new EFCoreStorage(OptionsAction, new EFCoreStorageOptions
        {
            CountersAggregationInterval = new TimeSpan(1),
        });
        storage.RegisterDatabaseInitializer(context => context.Database.EnsureCreated());
        var instance = new CountersAggregator(storage);
        var source = new CancellationTokenSource();
        if (cancelled)
            source.Cancel();
        var context = new BackgroundProcessContext(
            "1", storage, new Dictionary<string, object>(), default, source.Token, default, default);
        return (instance, context);
    }
}
