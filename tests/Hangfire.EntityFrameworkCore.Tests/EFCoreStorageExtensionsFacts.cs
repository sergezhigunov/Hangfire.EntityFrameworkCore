﻿using System;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public static class EFCoreStorageExtensionsFacts
    {
        [Fact]
        public static void UseEFCoreStorage_Throws_WhenConfigurationParameterIsNull()
        {
            IGlobalConfiguration configuration = null;
            void OptionsAction(DbContextOptionsBuilder builder) { }
            var options = new EFCoreStorageOptions();

            Assert.Throws<ArgumentNullException>(nameof(configuration),
                () => configuration.UseEFCoreStorage(OptionsAction));

            Assert.Throws<ArgumentNullException>(nameof(configuration),
                () => configuration.UseEFCoreStorage(OptionsAction, options));
        }

        [Fact]
        public static void UseEFCoreStorage_Throws_WhenOptionsActionParameterIsNull()
        {
            var configuration = new Mock<IGlobalConfiguration>().Object;
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
            var configuration = new Mock<IGlobalConfiguration>().Object;
            void OptionsAction(DbContextOptionsBuilder builder) { }
            EFCoreStorageOptions options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => configuration.UseEFCoreStorage(OptionsAction, options));
        }

        [Fact]
        public static void UseEFCoreStorage_CompletesSuccesfully()
        {
            var configurationMock = new Mock<IGlobalConfiguration>();
            var configuration = configurationMock.Object;
            bool exposed = false;
            void OptionsAction(DbContextOptionsBuilder builder) => exposed = true;
            var options = new EFCoreStorageOptions();

            var result = configuration.UseEFCoreStorage(OptionsAction, options);

            Assert.NotNull(result);
            var genericConfiguration =
                Assert.IsAssignableFrom<IGlobalConfiguration<EFCoreStorage>>(result);
            Assert.NotNull(genericConfiguration.Entry);
            Assert.True(exposed);
        }

        [Fact]
        public static void UseEFCoreStorage_CompletesSuccesfully_WithDefaultOptions()
        {
            var configurationMock = new Mock<IGlobalConfiguration>();
            var configuration = configurationMock.Object;
            bool optionsActionExposed = false;
            void OptionsAction(DbContextOptionsBuilder builder) => optionsActionExposed = true;

            var result = configuration.UseEFCoreStorage(OptionsAction);

            Assert.NotNull(result);
            var genericConfiguration =
                Assert.IsAssignableFrom<IGlobalConfiguration<EFCoreStorage>>(result);
            Assert.NotNull(genericConfiguration.Entry);
            Assert.True(optionsActionExposed);
        }

        [Fact]
        public static void UseQueueProvider_Throws_WhenStorageParameterIsNull()
        {
            EFCoreStorage storage = null;
            var provider = new Mock<IPersistentJobQueueProvider>().Object;
            var queues = new[] { EnqueuedState.DefaultQueue, };

            Assert.Throws<ArgumentNullException>(nameof(storage),
                () => storage.UseQueueProvider(provider, queues));
        }

        [Fact]
        public static void UseQueueProvider_Throws_WhenProviderParameterIsNull()
        {
            var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
            IPersistentJobQueueProvider provider = null;
            var queues = new[] { EnqueuedState.DefaultQueue, };

            Assert.Throws<ArgumentNullException>(nameof(provider),
                () => storage.UseQueueProvider(provider, queues));
        }

        [Fact]
        public static void UseQueueProvider_Throws_WhenQueuesParameterIsNull()
        {
            var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
            var provider = new Mock<IPersistentJobQueueProvider>().Object;
            string[] queues = null;

            Assert.Throws<ArgumentNullException>(nameof(queues),
                () => storage.UseQueueProvider(provider, queues));
        }

        [Fact]
        public static void UseQueueProvider_Throws_WhenQueuesParameterIsEmpty()
        {
            var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
            var provider = new Mock<IPersistentJobQueueProvider>().Object;
            var queues = Array.Empty<string>();

            Assert.Throws<ArgumentException>(nameof(queues),
                () => storage.UseQueueProvider(provider, queues));
        }

        [Fact]
        public static void UseQueueProvider_RegistersSpecifiedQueueProviderCorrectly()
        {
            var storage = new EFCoreStorage(x => { }, new EFCoreStorageOptions());
            var provider = new Mock<IPersistentJobQueueProvider>().Object;
            var queues = new[] { "test1", "test2" };

            var result = storage.UseQueueProvider(provider, queues);

            Assert.Same(storage, result);
            var providers = storage.QueueProviders;
            Assert.Equal(2, providers.Count);
            Assert.Same(provider, providers["test1"]);
            Assert.Same(provider, providers["test2"]);
        }
    }
}
