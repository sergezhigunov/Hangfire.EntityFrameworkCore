using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EFCoreStorageFacts : EFCoreStorageTest
    {
        [Fact]
        public static void Ctor_Throws_WhenContextOptionsActionParameterIsNull()
        {
            Action<DbContextOptionsBuilder> optionsAction = null;
            var options = new EFCoreStorageOptions();

            Assert.Throws<ArgumentNullException>(nameof(optionsAction),
                () => new EFCoreStorage(optionsAction, options));
        }

        [Fact]
        public void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            var contextOptions = OptionsActionStub;
            EFCoreStorageOptions options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new EFCoreStorage(OptionsAction, options));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var options = new EFCoreStorageOptions
            {
                DistributedLockTimeout = new TimeSpan(1, 0, 0),
                QueuePollInterval = new TimeSpan(0, 1, 0),
                JobExpirationCheckInterval = new TimeSpan(2, 0, 0),
            };

            var instance = new EFCoreStorage(OptionsActionStub, options);

            Assert.NotNull(Assert.IsType<DbContextOptions<HangfireContext>>(
                instance.GetFieldValue("_contextOptions")));
            Assert.Same(options, Assert.IsType<EFCoreStorageOptions>(
                instance.GetFieldValue("_options")));
            Assert.NotNull(instance.DefaultQueueProvider);
            Assert.Same(instance, instance.DefaultQueueProvider.GetFieldValue("_storage"));
            Assert.NotNull(instance.QueueProviders);
            Assert.Empty(instance.QueueProviders);
            Assert.Equal(options.DistributedLockTimeout, instance.DistributedLockTimeout);
            Assert.Equal(options.QueuePollInterval, instance.QueuePollInterval);
            Assert.Equal(options.JobExpirationCheckInterval, instance.JobExpirationCheckInterval);
        }

        [Fact]
        public void GetConnection_ReturnsCorrectResult()
        {
            var options = new EFCoreStorageOptions();
            var instance = new EFCoreStorage(OptionsAction, options);

            var result = instance.GetConnection();

            Assert.NotNull(result);
            var connection = Assert.IsType<EFCoreStorageConnection>(result);

            Assert.Same(instance,
                Assert.IsType<EFCoreStorage>(
                    connection.GetFieldValue("_storage")));
        }

        [Fact]
        public void GetMonitoringApi_ReturnsCorrectResult()
        {
            var options = new EFCoreStorageOptions();
            var instance = new EFCoreStorage(OptionsAction, options);

            var result = instance.GetMonitoringApi();

            Assert.NotNull(result);
            var api = Assert.IsType<EFCoreStorageMonitoringApi>(result);

            Assert.Same(instance,
                Assert.IsType<EFCoreStorage>(result.GetFieldValue("_storage")));
        }

        [Fact]
        public void CreateContext_CreatesInstance()
        {
            var instance = Storage.CreateContext();
            Assert.NotNull(instance);
            instance.Dispose();
        }

        [Fact]
        public void UseContext_Throws_WhenActionParameterIsNull()
        {
            Action<HangfireContext> action = null;

            Assert.Throws<ArgumentNullException>(nameof(action),
                () => Storage.UseContext(action));
        }

        [Fact]
        public void UseContext_InvokesAction()
        {
            bool exposed = false;
            Action<HangfireContext> action = context => exposed = true;

            Storage.UseContext(action);

            Assert.True(exposed);
        }

        [Fact]
        public void UseContextGeneric_Throws_WhenFuncParameterIsNull()
        {
            Func<HangfireContext, bool> func = null;

            Assert.Throws<ArgumentNullException>(nameof(func),
                () => Storage.UseContext(func));
        }

        [Fact]
        public void UseContextGeneric_InvokesFunc()
        {
            bool exposed = false;
            Func<HangfireContext, bool> func = context => exposed = true;

            var result = Storage.UseContext(func);

            Assert.True(exposed);
            Assert.True(result);
        }

        [Fact]
        public static void GetQueueProvider_Throws_WhenQueueParameterIsNull()
        {
            var storage = new EFCoreStorage(OptionsActionStub, new EFCoreStorageOptions());
            string queue = null;

            Assert.Throws<ArgumentNullException>(nameof(queue),
                () => storage.GetQueueProvider(queue));
        }

        [Fact]
        public static void GetQueueProvider_ReturnsDefaultProvider_WhenProviderIsNotRegistered()
        {
            var storage = new EFCoreStorage(OptionsActionStub, new EFCoreStorageOptions());
            var queue = "queue";

            var result = storage.GetQueueProvider(queue);

            Assert.NotNull(result);
            Assert.Same(storage.DefaultQueueProvider, result);
        }

        [Fact]
        public static void GetQueueProvider_ReturnsRegisteredProvider()
        {
            var storage = new EFCoreStorage(OptionsActionStub, new EFCoreStorageOptions());
            var dictionary = storage.QueueProviders;
            var provider = new Mock<IPersistentJobQueueProvider>().Object;
            var queue = "queue";
            dictionary[queue] = provider;

            var result = storage.GetQueueProvider(queue);

            Assert.NotNull(result);
            Assert.Same(provider, result);
        }

        [Fact]
        public static void GetComponents_ReturnsAllNeededComponents()
        {
            var storage = new EFCoreStorage(OptionsActionStub, new EFCoreStorageOptions());

            var result = storage.GetComponents();

            var componentTypes = result.Select(x => x.GetType()).ToArray();
            Assert.Contains(typeof(ExpirationManager), componentTypes);
        }
    }
}
