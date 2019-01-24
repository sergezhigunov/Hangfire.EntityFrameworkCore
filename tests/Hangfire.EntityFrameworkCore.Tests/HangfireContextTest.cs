using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
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
        private readonly SqliteConnection _connection;
        private protected DbContextOptions<HangfireContext> Options { get; }

        protected HangfireContextTest()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            Options = new DbContextOptionsBuilder<HangfireContext>().
                UseSqlite(_connection).
                Options;
            using (var context = new HangfireContext(Options))
                context.GetService<IRelationalDatabaseCreator>().CreateTables();
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
            _connection.Close();
        }
    }
}
