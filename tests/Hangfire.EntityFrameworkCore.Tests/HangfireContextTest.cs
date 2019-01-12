using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.Tests
{
    [ExcludeFromCodeCoverage]
    public abstract class HangfireContextTest
    {
        private protected DbContextOptions<HangfireContext> Options { get; }

        protected HangfireContextTest()
        {
            Options = new DbContextOptionsBuilder<HangfireContext>().
                UseInMemoryDatabase(Guid.NewGuid().ToString()).
                Options;
        }

        private protected void UseContext(Action<HangfireContext> action)
        {
            Options.UseContext(action);
        }

        private protected void UseContextSavingChanges(Action<HangfireContext> action)
        {
            UseContext(context =>
            {
                action(context);
                context.SaveChanges();
            });
        }
    }
}
