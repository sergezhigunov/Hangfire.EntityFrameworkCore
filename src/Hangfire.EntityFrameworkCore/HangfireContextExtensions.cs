using System;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal static class HangfireContextExtensions
    {
        public static void UseContext(
            this DbContextOptions<HangfireContext> options,
            Action<HangfireContext> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            using (var context = options.CreateContext())
                action(context);
        }

        public static T UseContext<T>(
            this DbContextOptions<HangfireContext> options,
            Func<HangfireContext, T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            using (var context = options.CreateContext())
                return func(context);
        }

        public static HangfireContext CreateContext(
            this DbContextOptions<HangfireContext> options)
        {
            return new HangfireContext(options);
        }
    }
}
