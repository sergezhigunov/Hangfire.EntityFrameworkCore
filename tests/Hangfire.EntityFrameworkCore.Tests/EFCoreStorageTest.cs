using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Hangfire.Common;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.Tests;

[ExcludeFromCodeCoverage]
public abstract class EFCoreStorageTest : DbContextOptionsTest
{
    private EFCoreStorage _storage;
    private EFCoreStorage _factoryStorage;

    private protected EFCoreStorage Storage =>
        LazyInitializer.EnsureInitialized(ref _storage,
            () =>
            {
                var storage = new EFCoreStorage(OptionsAction, new EFCoreStorageOptions { UseSlidingInvisibilityTimeout = true });
                storage.RegisterDatabaseInitializer(
                    context => context.Database.EnsureCreated());
                return storage;
            });

    private protected EFCoreStorage FactoryStorage =>
        LazyInitializer.EnsureInitialized(ref _factoryStorage,
            () => new EFCoreStorage(
                CreateInMemoryContext,
                new EFCoreStorageOptions { UseSlidingInvisibilityTimeout = true }));

    private protected static InvocationData InvocationDataStub { get; } =
        new InvocationData(null, null, null, string.Empty);

    private protected static string[] EmptyArrayStub { get; } =
        Array.Empty<string>();

    private protected static IDictionary<string, string> EmptyDictionaryStub { get; } =
        new Dictionary<string, string>();

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
        return InvocationData.SerializeJob(job);
    }

    private protected EFCoreStorage CreateStorageStub()
    {
        var options = new DbContextOptions<HangfireContext>();
        return new EFCoreStorage(OptionsAction, new EFCoreStorageOptions { UseSlidingInvisibilityTimeout = true });
    }

    private protected static Action<DbContextOptionsBuilder> OptionsActionStub { get; } =
        builder => { };

    public static void SampleMethod(string _) { }
}
