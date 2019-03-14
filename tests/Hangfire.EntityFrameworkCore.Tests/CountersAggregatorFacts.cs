using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
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
        public void Execute_Throws_WhenCancellationTokenRequested()
        {
            var storage = new EFCoreStorage(OptionsAction, new EFCoreStorageOptions
            {
                CountersAggregationInterval = new TimeSpan(1),
            });
            storage.RegisterDatabaseInitializer(context => context.Database.EnsureCreated());
            var instance = new CountersAggregator(storage);
            var source = new CancellationTokenSource(0);

            Assert.Throws<OperationCanceledException>(
                () => instance.Execute(source.Token));
        }

        [Fact]
        public void Execute_DoWorkCorrectly()
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

            var storage = new EFCoreStorage(OptionsAction, new EFCoreStorageOptions
            {
                CountersAggregationInterval = new TimeSpan(1),
            });
            var instance = new CountersAggregator(storage);
            var source = new CancellationTokenSource();

            instance.Execute(source.Token);

            UseContext(context =>
            {
                var result = context.Set<HangfireCounter>().ToArray();

                Assert.Equal(3, result.Length);
                Assert.Equal(10, result.Single(x => x.Key == "counter1").Value);
                Assert.Equal(-20, result.Single(x => x.Key == "counter2").Value);
                Assert.Equal(99, result.Single(x => x.Key == "counter3").Value);
            });
        }
    }
}
