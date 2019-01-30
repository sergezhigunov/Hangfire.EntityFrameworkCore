using System;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class EntityFrameworkCoreJobStorageFacts : HangfireContextTest
    {
        [Fact]
        public void Ctor_Throws_IfOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new EntityFrameworkCoreJobStorage(options));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var options = new DbContextOptions<HangfireContext>();

            var instance = new EntityFrameworkCoreJobStorage(options);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    instance.GetFieldValue("_options")));
        }

        [Fact]
        public void GetConnection_ReturnsCorrectResult()
        {
            var options = new DbContextOptions<HangfireContext>();
            var instance = new EntityFrameworkCoreJobStorage(options);

            var result = instance.GetConnection();

            Assert.NotNull(result);
            var connection = Assert.IsType<EntityFrameworkCoreJobStorageConnection>(result);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    connection.GetFieldValue("_options")));
        }

        [Fact]
        public void GetMonitoringApi_ReturnsCorrectResult()
        {
            var options = new DbContextOptions<HangfireContext>();
            var instance = new EntityFrameworkCoreJobStorage(options);

            var result = instance.GetMonitoringApi();

            Assert.NotNull(result);
            var api = Assert.IsType<EntityFrameworkCoreJobStorageMonitoringApi>(result);

            Assert.Same(options,
                Assert.IsType<DbContextOptions<HangfireContext>>(
                    api.GetFieldValue("_options")));
        }
    }
}
