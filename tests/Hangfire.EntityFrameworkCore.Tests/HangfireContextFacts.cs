using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public class HangfireContextFacts : DbContextOptionsTest
    {
        [Fact]
        public static void Ctor_Throws_WhenOptionsParameterIsNull()
        {
            DbContextOptions options = null;
            string schema = string.Empty;

            Assert.Throws<ArgumentNullException>(nameof(options),
                () => new HangfireContext(options, schema));
        }

        [Fact]
        public static void Ctor_Throws_WhenSchemaParameterIsNull()
        {
            DbContextOptions options = new DbContextOptionsBuilder<HangfireContext>().Options;
            string schema = null;

            Assert.Throws<ArgumentNullException>(nameof(schema),
                () => new HangfireContext(options, schema));
        }

        [Theory]
        [InlineData("")]
        [InlineData("hangfire")]
        [InlineData("Hangfire")]
        public void Ctor_CreatesInstance(string schema)
        {
            var builder = new DbContextOptionsBuilder<HangfireContext>();
            OptionsAction(builder);
            DbContextOptions options = builder.Options;

            using var context = new HangfireContext(options, schema);
            Assert.Same(schema, context.Schema);
            var model = context.Model;
            Assert.NotNull(model);

            var actualSchema = context.Model
#if NETCOREAPP3_1 || NET5_0
                .GetDefaultSchema()
#else
                .Relational()
                .DefaultSchema
#endif
                ?? string.Empty;
            Assert.Equal(schema, actualSchema);
        }
    }
}
