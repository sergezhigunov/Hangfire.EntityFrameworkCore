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

        protected DbContextOptionsTest()
        {
        }

        private protected void UseContext(Action<HangfireContext> action)
        {
            using (var context = new HangfireContext(Options))
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
