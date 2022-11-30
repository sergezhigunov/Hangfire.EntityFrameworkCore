using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;

namespace Hangfire.EntityFrameworkCore.Tests;

[ExcludeFromCodeCoverage]
public abstract class DbContextOptionsTest : IDisposable
{
    public static LoggerFactory LoggerFactory { get; } = new LoggerFactory(new[]
    {
            new DebugLoggerProvider(),
        });

    private SqliteConnection _connection;
    private bool _disposed = false;

    private SqliteConnection Connection =>
        LazyInitializer.EnsureInitialized(ref _connection, () =>
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var builder = new DbContextOptionsBuilder<HangfireContext>();
            builder.UseSqlite(connection);
            return connection;
        });

    private protected void OptionsAction(DbContextOptionsBuilder builder)
    {
        builder.UseLoggerFactory(LoggerFactory);
        builder.UseSqlite(Connection);
    }

    protected DbContextOptionsTest()
    {
    }

    private protected void UseContext(Action<HangfireContext> action)
    {
        var builder = new DbContextOptionsBuilder<HangfireContext>();
        OptionsAction(builder);
        using var context = CreateContext(builder);
        action(context);
    }

    private static HangfireContext CreateContext(DbContextOptionsBuilder<HangfireContext> builder)
    {
        var context = new HangfireContext(builder.Options, string.Empty);
        context.Database.EnsureCreated();
        return context;
    }

    private protected DbContext CreateInMemoryContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder().UseSqlite(Connection);
        return new HangfireContext(optionsBuilder.Options, string.Empty);
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
