using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using Hangfire.Common;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.Tests
{
    [ExcludeFromCodeCoverage]
    public abstract class EFCoreStorageTest : DbContextOptionsTest
    {
        private EFCoreStorage _storage;

        private protected EFCoreStorage Storage =>
            LazyInitializer.EnsureInitialized(ref _storage,
                () =>
                {
                    var storage = new EFCoreStorage(OptionsAction, new EFCoreStorageOptions());
                    storage.RegisterDatabaseInitializer(
                        context => context.Database.EnsureCreated());
                    return storage;
                });

        private protected static string InvocationDataStub { get; } =
            JobHelper.ToJson(new InvocationData(null, null, null, string.Empty));

        private protected static string EmptyArrayStub { get; } =
            JobHelper.ToJson(Array.Empty<string>());

        private protected static string EmptyDictionaryStub { get; } =
            JobHelper.ToJson(new Dictionary<string, string>());

        protected EFCoreStorageTest()
        {
        }

        private protected static string CreateInvocationData(Expression<Action> methodCall)
        {
            var job = Job.FromExpression(methodCall);
            return CreateInvocationData(job);
        }

        private protected static string CreateInvocationData(Job job)
        {
            return JobHelper.ToJson(InvocationData.Serialize(job));
        }

        private protected EFCoreStorage CreateStorageStub()
        {
            var options = new DbContextOptions<HangfireContext>();
            return new EFCoreStorage(OptionsAction, new EFCoreStorageOptions());
        }

        private protected static Action<DbContextOptionsBuilder> OptionsActionStub { get; } =
            builder => { };

        public static void SampleMethod(string _) { }
    }
}
