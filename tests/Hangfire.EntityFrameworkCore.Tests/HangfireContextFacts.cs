using System;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class HangfireContextFacts : DbContextOptionsTest
    {
        [Fact]
        public static void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions options = null;
            string defaultSchema = "Hangfire";

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new HangfireContext(null, defaultSchema));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            var builder = new DbContextOptionsBuilder<HangfireContext>();
            string defaultSchema = "Hangfire";
            OptionsAction(builder);
            using (var context = new HangfireContext(builder.Options, defaultSchema))
            {
                Assert.NotNull(context.Model);
            }
        }
    }
}
