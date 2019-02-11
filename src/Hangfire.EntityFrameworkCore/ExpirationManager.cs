using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
#pragma warning disable 618
    internal class ExpirationManager : IServerComponent
#pragma warning restore 618
    {
        private const string LockKey = "locks:expirationmanager";
        private static readonly TimeSpan s_lockTimeout = new TimeSpan(0, 5, 0);
        private static readonly Type s_expirableType = typeof(IExpirable);
        private static readonly MethodInfo s_setMethodDefinition =
            typeof(HangfireContext).GetMethod(nameof(DbContext.Set));
        private readonly ILog _logger = LogProvider.For<ExpirationManager>();
        private readonly EFCoreStorage _storage;

        public ExpirationManager(EFCoreStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _storage.UseContextSavingChanges(context =>
            {
                var expirableSets =
                    from entityType in context.Model.GetEntityTypes()
                    let clrType = entityType.ClrType
                    where s_expirableType.IsAssignableFrom(clrType)
                    let method = s_setMethodDefinition.MakeGenericMethod(clrType)
                    select new
                    {
                        TableName = clrType.Name,
                        DbSet = (IQueryable<IExpirable>)method.Invoke(context, null),
                    };

                var now = DateTime.UtcNow;
                foreach (var item in expirableSets)
                {
                    _logger.Debug(
                        $"Removing outdated records from the '{item.TableName}' table...");

                    UseLock(() =>
                    {
                        context.RemoveRange(item.DbSet.Where(x => x.ExpireAt < now));
                        context.SaveChanges();
                    });

                    _logger.Trace($"Outdated records removed from the '{item.TableName}' table.");
                }
            });

            cancellationToken.WaitHandle.WaitOne(_storage.JobExpirationCheckInterval);
        }

        private void UseLock(Action action)
        {
            using (var connection = _storage.GetConnection())
            {
                try
                {
                    using (var @lock = connection.AcquireDistributedLock(LockKey, s_lockTimeout))
                        action.Invoke();
                }
                catch (DistributedLockTimeoutException exception)
                when (exception.Resource == LockKey)
                {
                    _logger.Log(LogLevel.Debug, () =>
                        $@"An exception was thrown during acquiring distributed lock on the {
                            LockKey
                        } resource within {
                            s_lockTimeout.TotalSeconds
                        } seconds. Outdated records were not removed.
It will be retried in {_storage.JobExpirationCheckInterval.TotalSeconds} seconds.",
                        exception);
                }
            }
        }
    }
}
