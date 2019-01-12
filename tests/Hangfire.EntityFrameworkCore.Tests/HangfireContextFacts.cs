using System;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class HangfireContextFacts : HangfireContextTest
    {
        [Fact]
        public static void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            Assert.Throws<ArgumentNullException>("options",  () => new HangfireContext(null));
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
