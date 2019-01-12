using System;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class HangfireContextExtensionsFacts : HangfireContextTest
    {
        [Fact]
        public static void CreateContext_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => options.CreateContext());
        }

        [Fact]
        public void CreateContext_CreatesInstance()
        {
            var instance = Options.CreateContext();
            Assert.NotNull(instance);
            instance.Dispose();
        }

        [Fact]
        public static void UseContext_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => options.UseContext(x => { }));
        }

        [Fact]
        public void UseContext_Throws_WhenActionParameterIsNull()
        {
            Action<HangfireContext> action = null;

            Assert.Throws<ArgumentNullException>(nameof(action),
                () => Options.UseContext(action));
        }

        [Fact]
        public void UseContext_InvokesAction()
        {
            bool exposed = false;
            Action<HangfireContext> action = context => exposed = true;

            Options.UseContext(action);

            Assert.True(exposed);
        }

        [Fact]
        public static void UseContextGeneric_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions<HangfireContext> options = null;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => options.UseContext<object>(x => default));
        }


        [Fact]
        public void UseContextGeneric_Throws_WhenFuncParameterIsNull()
        {
            Func<HangfireContext, bool> func = null;

            Assert.Throws<ArgumentNullException>(nameof(func),
                () => Options.UseContext(func));
        }

        [Fact]
        public void UseContextGeneric_InvokesFunc()
        {
            bool exposed = false;
            Func<HangfireContext, bool> func = context => exposed = true;

            var result = Options.UseContext(func);

            Assert.True(exposed);
            Assert.True(result);
        }
    }
}
