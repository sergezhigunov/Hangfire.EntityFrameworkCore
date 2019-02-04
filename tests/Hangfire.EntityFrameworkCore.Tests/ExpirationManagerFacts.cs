using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class ExpirationManagerFacts : EFCoreStorageTest
    {
        [Fact]
        public void Ctor_Throws_WhenStorageParameterIsNull()
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
                Assert.False(context.Counters.Any());
                Assert.False(context.Jobs.Any());
                Assert.False(context.Lists.Any());
                Assert.False(context.Sets.Any());
                Assert.False(context.Hashes.Any());
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
                Assert.Equal(1, context.Counters.Count());
                Assert.Equal(1, context.Jobs.Count());
                Assert.Equal(1, context.Lists.Count());
                Assert.Equal(1, context.Sets.Count());
                Assert.Equal(1, context.Hashes.Count());
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
                Assert.Equal(1, context.Counters.Count());
                Assert.Equal(1, context.Jobs.Count());
                Assert.Equal(1, context.Lists.Count());
                Assert.Equal(1, context.Sets.Count());
                Assert.Equal(1, context.Hashes.Count());
            });
        }

        private void CreateExpirationEntries(DateTime? expireAt)
        {
            var now = DateTime.UtcNow;

            UseContextSavingChanges(context =>
            {
                context.Counters.Add(new HangfireCounter
                {
                    Key = "test",
                    ExpireAt = expireAt,
                });

                context.Jobs.Add(new HangfireJob
                {
                    CreatedAt = now,
                    InvocationData = CreateInvocationData(() => SampleMethod("test")),
                    ExpireAt = expireAt,
                });

                context.Lists.Add(new HangfireList
                {
                    Key = "test",
                    ExpireAt = expireAt,
                });

                context.Sets.Add(new HangfireSet
                {
                    Key = "test",
                    Value = "test",
                    CreatedAt = now,
                    ExpireAt = expireAt,
                });

                context.Hashes.Add(new HangfireHash
                {
                    Key = "test",
                    Field = "test",
                    ExpireAt = expireAt,
                });
            });
        }
    }
}
