using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.Tests;

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
    [MemberData(nameof(Schemata))]
    public void Ctor_CreatesInstance(string schema)
    {
        var builder = new DbContextOptionsBuilder<HangfireContext>();
        OptionsAction(builder);
        DbContextOptions options = builder.Options;

        using var context = new HangfireContext(options, schema);
        Assert.Same(schema, context.Schema);
        var model = context.Model;
        Assert.NotNull(model);

        var expectedSchema = schema == string.Empty ? null : schema;
        Assert.All(
            model.GetEntityTypes(),
            entity => Assert.Equal(expectedSchema, entity.GetSchema()));

        var script = context.Database.GenerateCreateScript();
        Assert.NotNull(script);
    }

    public static TheoryData<string> Schemata()
    {
        return
        [
            string.Empty,
            "hangfire",
            "Hangfire",
            "HANGFIRE",
        ];
    }
}
