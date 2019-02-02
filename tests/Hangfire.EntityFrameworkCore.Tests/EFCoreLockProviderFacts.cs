using System;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EFCoreLockProviderFacts : EFCoreStorageTest
    {
        [Fact]
        public static void Ctor_Throws_WhenStorageParameterIsNull()
        {
            EFCoreStorage storage = null;

            Assert.Throws<ArgumentNullException>(nameof(storage),
                () => new EFCoreLockProvider(storage));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var storage = CreateStorageStub();

            var instance = new EFCoreLockProvider(storage);

            Assert.Same(storage, Assert.IsType<EFCoreStorage>(instance.GetFieldValue("_storage")));
        }

        [Fact]
        public static void Acquire_Throws_WhenResourceParameterIsNull()
        {
            var instance = CreateStub();
            string resource = null;
            var timeout = new TimeSpan();

            Assert.Throws<ArgumentNullException>(nameof(resource),
                () => instance.Acquire(resource, timeout));
        }

        [Fact]
        public static void Acquire_Throws_WhenResourceParameterIsEmpty()
        {
            var instance = CreateStub();
            string resource = string.Empty;
            var timeout = new TimeSpan();

            Assert.Throws<ArgumentException>(nameof(resource),
                () => instance.Acquire(resource, timeout));
        }

        [Fact]
        public static void Acquire_Throws_WhenTimeoutParameterIsNegative()
        {
            var instance = CreateStub();
            string resource = "resource";
            var timeout = new TimeSpan(-1);

            Assert.Throws<ArgumentOutOfRangeException>(nameof(timeout),
                () => instance.Acquire(resource, timeout));
        }

        [Fact]
        public void Acquire_Throws_WhenExisingLockIsActual()
        {
            var instance = CreateInstance();
            string resource = "resource";
            var timeout = new TimeSpan(1);
            var hangfireLock = new HangfireLock
            {
                Id = resource,
                AcquiredAt = DateTime.UtcNow.AddMinutes(1),
            };
            UseContextSavingChanges(context => context.Locks.Add(hangfireLock));

            var exception = Assert.Throws<DistributedLockTimeoutException>(
                () => instance.Acquire(resource, timeout));

            Assert.Equal(resource, exception.Resource);

            UseContext(context =>
            {
                var actual = Assert.Single(context.Locks);
                Assert.Equal(hangfireLock.Id, actual.Id);
                Assert.Equal(hangfireLock.AcquiredAt, actual.AcquiredAt);
            });
        }

        [Fact]
        public void Acquire_CompletesSuccessfully_WhenLockNotExists()
        {
            var instance = CreateInstance();
            string resource = "resource";
            var timeout = new TimeSpan(0, 0, 10);

            instance.Acquire(resource, timeout);

            UseContext(context =>
            {
                var actual = Assert.Single(context.Locks);
                Assert.Equal(resource, actual.Id);
                Assert.NotEqual(default, actual.AcquiredAt);
            });
        }

        [Fact]
        public void Acquire_CompletesSuccessfully_WhenExisingLockIsOutdated()
        {
            var instance = CreateInstance();
            string resource = "resource";
            var timeout = new TimeSpan(0, 0, 10);
            var hangfirelock = new HangfireLock
            {
                Id = resource,
                AcquiredAt = DateTime.UtcNow.AddMinutes(-1),
            };
            UseContextSavingChanges(context => context.Locks.Add(hangfirelock));

            instance.Acquire(resource, timeout);

            UseContext(context =>
            {
                var actual = Assert.Single(context.Locks);
                Assert.Equal(resource, actual.Id);
                Assert.True(hangfirelock.AcquiredAt < actual.AcquiredAt);
            });
        }

        [Fact]
        public static void Release_Throws_WhenResourceParameterIsNull()
        {
            var instance = CreateStub();
            string resource = null;

            Assert.Throws<ArgumentNullException>(nameof(resource),
                () => instance.Release(resource));
        }

        [Fact]
        public void Release_CompletesSuccessfully_WhenLockExists()
        {
            var instance = CreateInstance();
            string resource = "resource";
            UseContextSavingChanges(context =>
            {
                context.Locks.Add(new HangfireLock
                {
                    Id = resource,
                    AcquiredAt = DateTime.UtcNow,
                });
            });

            instance.Release(resource);

            UseContext(context => Assert.Empty(context.Locks));
        }

        [Fact]
        public void Release_CompletesSuccessfully_WhenLockNotExists()
        {
            var instance = CreateInstance();
            string resource = "resource";

            instance.Release(resource);

            UseContext(context => Assert.Empty(context.Locks));
        }

        private static EFCoreLockProvider CreateStub()
        {
            var options = new DbContextOptions<HangfireContext>();
            var storage = new EFCoreStorage(options, new EFCoreStorageOptions());
            return new EFCoreLockProvider(storage);
        }

        private EFCoreLockProvider CreateInstance()
        {
            var storage = new EFCoreStorage(Options, new EFCoreStorageOptions
            {
                DistributedLockTimeout = new TimeSpan(0, 0, 1),
            });
            return new EFCoreLockProvider(storage);
        }
    }
}
