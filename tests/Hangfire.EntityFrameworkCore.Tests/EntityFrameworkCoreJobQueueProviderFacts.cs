using System;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EntityFrameworkCoreJobQueueProviderFacts : HangfireContextTest
    {
        [Fact]
        public void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new EntityFrameworkCoreJobQueueProvider(options));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var options = new DbContextOptions<HangfireContext>();

            var instance = new EntityFrameworkCoreJobQueueProvider(options);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    instance.GetFieldValue("_options")));
        }

        [Fact]
        public void GetJobQueue_CreatesInstance()
        {
            var instance = new EntityFrameworkCoreJobQueueProvider(Options);

            var result = instance.GetJobQueue();

            Assert.NotNull(result);
            Assert.IsType<EntityFrameworkCoreJobQueue>(result);
        }

        [Fact]
        public void GetMonitoringApi_CreatesInstance()
        {
            var instance = new EntityFrameworkCoreJobQueueProvider(Options);

            var result = instance.GetMonitoringApi();

            Assert.NotNull(result);
            Assert.IsType<EntityFrameworkCoreJobQueueMonitoringApi>(result);
        }
    }
}
