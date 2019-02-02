using System;
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
                () => new EFCoreStorage(OptionsAction, new EFCoreStorageOptions()));

        protected EFCoreStorageTest()
        {
        }

        private protected static InvocationData CreateInvocationData(Expression<Action> methodCall)
        {
            var job = Job.FromExpression(methodCall);
            return CreateInvocationData(job);
        }

        private protected static InvocationData CreateInvocationData(Job job)
        {
            return InvocationData.Serialize(job);
        }

        private protected EFCoreStorage CreateStorageStub()
        {
            var options = new DbContextOptions<HangfireContext>();
            return new EFCoreStorage(OptionsAction, new EFCoreStorageOptions());
        }

        private protected static Action<DbContextOptionsBuilder> OptionsActionStub { get; } =
            builder => { };

        [SuppressMessage("Usage", "xUnit1013")]
        public static void SampleMethod(string value) { }
    }
}
