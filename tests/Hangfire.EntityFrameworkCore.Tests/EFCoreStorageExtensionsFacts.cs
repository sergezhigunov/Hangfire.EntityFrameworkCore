using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Hangfire.EntityFrameworkCore.Tests;

public static class EFCoreStorageExtensionsFacts
{
    [Fact]
    public static void UseEFCoreStorage_Throws_WhenConfigurationParameterIsNull()
    {
        IGlobalConfiguration configuration = null;
        static void OptionsAction(DbContextOptionsBuilder builder) { }
        var options = new EFCoreStorageOptions();

        Assert.Throws<ArgumentNullException>(nameof(configuration),
            () => configuration.UseEFCoreStorage(OptionsAction));

        Assert.Throws<ArgumentNullException>(nameof(configuration),
            () => configuration.UseEFCoreStorage(OptionsAction, options));
    }

    [Fact]
    public static void UseEFCoreStorage_Throws_WhenOptionsActionParameterIsNull()
    {
        var configuration = Mock.Of<IGlobalConfiguration>();
        Action<DbContextOptionsBuilder> optionsAction = null;
        var options = new EFCoreStorageOptions();

        Assert.Throws<ArgumentNullException>(nameof(optionsAction),
            () => configuration.UseEFCoreStorage(optionsAction));

        Assert.Throws<ArgumentNullException>(nameof(optionsAction),
            () => configuration.UseEFCoreStorage(optionsAction, options));
    }

    [Fact]
    public static void UseEFCoreStorage_Throws_WhenOptionsParameterIsNull()
    {
        var configuration = Mock.Of<IGlobalConfiguration>();
        static void OptionsAction(DbContextOptionsBuilder builder) { }
        EFCoreStorageOptions options = null;

        Assert.Throws<ArgumentNullException>(nameof(options),
            () => configuration.UseEFCoreStorage(OptionsAction, options));
    }

    [Fact]
    public static void UseEFCoreStorage_CompletesSuccesfully()
    {
        var configuration = Mock.Of<IGlobalConfiguration>();
        bool exposed = false;
        void OptionsAction(DbContextOptionsBuilder builder) => exposed = true;
        var options = new EFCoreStorageOptions();

        var result = configuration.UseEFCoreStorage(OptionsAction, options);

        Assert.NotNull(result);
        var genericConfiguration = Assert.IsType<IGlobalConfiguration<EFCoreStorage>>(result, exactMatch: false);
        Assert.NotNull(genericConfiguration.Entry);
        Assert.True(exposed);
    }

    [Fact]
    public static void UseEFCoreStorage_CompletesSuccesfully_WithDefaultOptions()
    {
        var configuration = Mock.Of<IGlobalConfiguration>();
        bool optionsActionExposed = false;
        void OptionsAction(DbContextOptionsBuilder builder) => optionsActionExposed = true;

        var result = configuration.UseEFCoreStorage(OptionsAction);

        Assert.NotNull(result);
        var genericConfiguration = Assert.IsType<IGlobalConfiguration<EFCoreStorage>>(result, exactMatch: false);
        Assert.NotNull(genericConfiguration.Entry);
        Assert.True(optionsActionExposed);
    }

    [Fact]
    public static void UseEFCoreStorageFactory_Throws_WhenConfigurationParameterIsNull()
    {
        IGlobalConfiguration configuration = null;
        static DbContext contextBuilder() => new Mock<DbContext>().Object;
        EFCoreStorageOptions options = new Mock<EFCoreStorageOptions>().Object;

        Assert.Throws<ArgumentNullException>(nameof(configuration),
            () => configuration.UseEFCoreStorage(contextBuilder, options));
    }

    [Fact]
    public static void UseEFCoreStorageFactory_Throws_WhenContextBuilderParameterIsNull()
    {
        var configuration = Mock.Of<IGlobalConfiguration>();
        Func<DbContext> contextBuilder = null;
        EFCoreStorageOptions options = new Mock<EFCoreStorageOptions>().Object;

        Assert.Throws<ArgumentNullException>(nameof(contextBuilder),
            () => configuration.UseEFCoreStorage(contextBuilder, options));
    }

    [Fact]
    public static void UseEFCoreStorageFactory_Throws_WhenOptionsParameterIsNull()
    {
        var configuration = Mock.Of<IGlobalConfiguration>();
        static DbContext contextBuilder() => new Mock<DbContext>().Object;
        EFCoreStorageOptions options = null;

        Assert.Throws<ArgumentNullException>(nameof(options),
            () => configuration.UseEFCoreStorage(contextBuilder, options));
    }

    [Fact]
    public static void UseEFCoreStorageFactory_CompletesSuccessfully()
    {
        var configuration = Mock.Of<IGlobalConfiguration>();
        var options = new EFCoreStorageOptions();
        static DbContext contextBuilder() => new Mock<DbContext>().Object;

        var result = configuration.UseEFCoreStorage(contextBuilder, options);

        Assert.NotNull(result);
        var genericConfiguration = Assert.IsType<IGlobalConfiguration<EFCoreStorage>>(result, exactMatch: false);
        Assert.NotNull(genericConfiguration.Entry);
    }

    [Fact]
    public static void UseDatabaseCreator_Throws_WhenStorageParameterIsNull()
    {
        var configuration = default(IGlobalConfiguration<EFCoreStorage>)!;

        Assert.Throws<ArgumentNullException>(nameof(configuration),
            () => configuration.UseDatabaseCreator());
    }

    [Fact]
    public static void UseDatabaseCreator_RegistersDatabaseCreatorCorrectly()
    {
        var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
        var configuration = Mock.Of<IGlobalConfiguration<EFCoreStorage>>(x => x.Entry == storage);

        var result = configuration.UseDatabaseCreator();

        Assert.Same(storage, result.Entry);
        var action = Assert.IsType<Action<HangfireContext>>(
            storage.GetFieldValue("_databaseInitializer"));
    }

    [Fact]
    public static void UseQueueProvider_Throws_WhenStorageParameterIsNull()
    {
        var configuration = default(IGlobalConfiguration<EFCoreStorage>);
        var provider = Mock.Of<IPersistentJobQueueProvider>();
        var queues = new[] { EnqueuedState.DefaultQueue, };

        Assert.Throws<ArgumentNullException>(nameof(configuration),
            () => configuration.UseQueueProvider(provider, queues));
    }

    [Fact]
    public static void UseQueueProvider_Throws_WhenProviderParameterIsNull()
    {
        var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
        var configuration = Mock.Of<IGlobalConfiguration<EFCoreStorage>>(x => x.Entry == storage);
        IPersistentJobQueueProvider provider = null;
        var queues = new[] { EnqueuedState.DefaultQueue, };

        Assert.Throws<ArgumentNullException>(nameof(provider),
            () => configuration.UseQueueProvider(provider, queues));
    }

    [Fact]
    public static void UseQueueProvider_Throws_WhenQueuesParameterIsNull()
    {
        var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
        var configuration = Mock.Of<IGlobalConfiguration<EFCoreStorage>>(x => x.Entry == storage);
        var provider = new Mock<IPersistentJobQueueProvider>().Object;
        string[] queues = null;

        Assert.Throws<ArgumentNullException>(nameof(queues),
            () => configuration.UseQueueProvider(provider, queues));
    }

    [Fact]
    public static void UseQueueProvider_Throws_WhenQueuesParameterIsEmpty()
    {
        var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
        var configuration = Mock.Of<IGlobalConfiguration<EFCoreStorage>>(x => x.Entry == storage);
        var provider = new Mock<IPersistentJobQueueProvider>().Object;
        var queues = Array.Empty<string>();

        Assert.Throws<ArgumentException>(nameof(queues),
            () => configuration.UseQueueProvider(provider, queues));
    }

    [Fact]
    public static void UseQueueProvider_RegistersSpecifiedQueueProviderCorrectly()
    {
        var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
        var configuration = Mock.Of<IGlobalConfiguration<EFCoreStorage>>(x => x.Entry == storage);
        var provider = new Mock<IPersistentJobQueueProvider>().Object;
        var queues = new[] { "test1", "test2" };

        var result = configuration.UseQueueProvider(provider, queues);

        Assert.Same(storage, result.Entry);
        var providers = storage.QueueProviders;
        Assert.Equal(2, providers.Count);
        Assert.Same(provider, providers["test1"]);
        Assert.Same(provider, providers["test2"]);
    }
}
