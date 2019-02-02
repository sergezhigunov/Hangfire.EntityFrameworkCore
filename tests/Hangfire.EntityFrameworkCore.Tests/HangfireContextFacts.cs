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

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new HangfireContext(null));
        }

        [Fact]
        public void Ctor_CreatesInstance()
        {
            using (var context = new HangfireContext(Options))
            {
                Assert.NotNull(context.Model);
            }
        }
    }
}
