using System;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class EFCoreStorage : JobStorage
    {
        private readonly DbContextOptions _options;

        public EFCoreStorage(
            DbContextOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override IStorageConnection GetConnection()
        {
            return new EFCoreStorageConnection(this);
        }

        public override IMonitoringApi GetMonitoringApi()
        {
            return new EFCoreStorageMonitoringApi(this);
        }

        internal void UseContext(Action<HangfireContext> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            using (var context = CreateContext())
                action(context);
        }

        internal void UseContextSavingChanges(Action<HangfireContext> action)
        {
            UseContext(context =>
            {
                action(context);
                context.SaveChanges();
            });
        }

        internal T UseContext<T>(Func<HangfireContext, T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            using (var context = CreateContext())
                return func(context);
        }

        internal T UseContextSavingChanges<T>(Func<HangfireContext, T> func)
        {
            return UseContext(context =>
            {
                var result = func(context);
                context.SaveChanges();
                return result;
            });
        }

        internal HangfireContext CreateContext()
        {
            return new HangfireContext(_options);
        }
    }
}
