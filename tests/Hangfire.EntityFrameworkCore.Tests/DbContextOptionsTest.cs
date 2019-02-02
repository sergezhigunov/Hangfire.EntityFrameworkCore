using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hangfire.EntityFrameworkCore.Tests
{
    [ExcludeFromCodeCoverage]
    public abstract class DbContextOptionsTest : IDisposable
    {
        private SqliteConnection _connection;
        private bool _disposed = false;

        private SqliteConnection Connection =>
            LazyInitializer.EnsureInitialized(ref _connection, () =>
            {
                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                var builder = new DbContextOptionsBuilder<HangfireContext>();
                builder.UseSqlite(connection);
                using (var context = new HangfireContext(builder.Options))
                    context.GetService<IRelationalDatabaseCreator>().CreateTables();
                return connection;
            });

        private protected void OptionsAction(DbContextOptionsBuilder builder)
        {
            builder.UseSqlite(Connection);
        }

        protected DbContextOptionsTest()
        {
        }

        private protected void UseContext(Action<HangfireContext> action)
        {
            var builder = new DbContextOptionsBuilder<HangfireContext>();
            OptionsAction(builder);
            using (var context = new HangfireContext(builder.Options))
                action(context);
        }

        private protected void UseContextSavingChanges(Action<HangfireContext> action)
        {
            UseContext(context =>
            {
                action.Invoke(context);
                context.SaveChanges();
            });
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
    }
}
