using System;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EFCoreJobQueueProviderFacts : EFCoreStorageTest
    {
        [Fact]
        public static void Ctor_Throws_WhenStorageParameterIsNull()
        {
            EFCoreStorage storage = null;

            Assert.Throws<ArgumentNullException>(nameof(storage),
                () => new EFCoreJobQueueProvider(storage));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var storage = CreateStorageStub();

            var instance = new EFCoreJobQueueProvider(storage);

            Assert.Same(storage, Assert.IsType<EFCoreStorage>(instance.GetFieldValue("_storage")));
        }

        [Fact]
        public void GetJobQueue_CreatesInstance()
        {
            var instance = new EFCoreJobQueueProvider(CreateStorageStub());

            var result = instance.GetJobQueue();

            Assert.NotNull(result);
            Assert.IsType<EFCoreJobQueue>(result);
        }

        [Fact]
        public void GetMonitoringApi_CreatesInstance()
        {
            var instance = new EFCoreJobQueueProvider(CreateStorageStub());

            var result = instance.GetMonitoringApi();

            Assert.NotNull(result);
            Assert.IsType<EFCoreJobQueueMonitoringApi>(result);
        }
    }
}
