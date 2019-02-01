using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using Hangfire.Common;
using Hangfire.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hangfire.EntityFrameworkCore.Tests
{
    [ExcludeFromCodeCoverage]
    public abstract class HangfireContextTest : IDisposable
    {
        private SqliteConnection _connection;
        private DbContextOptions _options;
        private bool _disposed = false;

        private SqliteConnection Connection =>
            LazyInitializer.EnsureInitialized(ref _connection,
                () => new SqliteConnection("DataSource=:memory:"));

        private protected DbContextOptions Options =>
            LazyInitializer.EnsureInitialized(ref _options, () =>
            {
                Connection.Open();
                var options = new DbContextOptionsBuilder<HangfireContext>().
                    UseSqlite(Connection).
                    Options;
                using (var context = new HangfireContext(options))
                    context.GetService<IRelationalDatabaseCreator>().CreateTables();
                return options;
            });

        protected HangfireContextTest()
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

        private protected void UseContext(Action<HangfireContext> action)
        {
            Options.UseContext(action);
        }

        private protected void UseContextSavingChanges(Action<HangfireContext> action)
        {
            Options.UseContextSavingChanges(action);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    _connection?.Close();
                _disposed = true;
            }
        }

        [ExcludeFromCodeCoverage]
        [SuppressMessage("Usage", "xUnit1013")]
        public static void SampleMethod(string value) { }
    }
}
