using System;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EntityFrameworkCoreLockProviderFacts : HangfireContextTest
    {
        [Fact]
        public static void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;
            TimeSpan timeout = default;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new EntityFrameworkCoreLockProvider(options, timeout));
        }

        [Fact]
        public static void Ctor_CreatesInstance()
        {
            var options = new DbContextOptions<HangfireContext>();
            var timeout = new TimeSpan(123);

            var instance = new EntityFrameworkCoreLockProvider(options, timeout);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    instance.GetFieldValue("_options")));
            Assert.Equal(timeout,
                Assert.IsType<TimeSpan>(
                    instance.GetFieldValue("_timeout")));
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
            var instance = CreateInstance(new TimeSpan(0, 0, 0));
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
            var instance = CreateInstance(default);
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
            var instance = CreateInstance(default);
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
            var instance = CreateInstance(default);
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
            var instance = CreateInstance(default);
            string resource = "resource";

            instance.Release(resource);

            UseContext(context => Assert.Empty(context.Locks));
        }

        private static EntityFrameworkCoreLockProvider CreateStub()
        {
            var options = new DbContextOptions<HangfireContext>();
            return new EntityFrameworkCoreLockProvider(options, default);
        }

        private EntityFrameworkCoreLockProvider CreateInstance(TimeSpan timeout)
        {
            return new EntityFrameworkCoreLockProvider(Options, timeout);
        }
    }
}
