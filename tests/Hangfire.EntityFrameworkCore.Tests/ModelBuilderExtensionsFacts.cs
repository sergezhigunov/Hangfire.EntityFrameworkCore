using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Hangfire.EntityFrameworkCore.Tests;

public class ModelBuilderExtensionsFacts
{
    [Fact]
    public void OnHangfireModelCreating_Throws_IfParametersAreInvalid()
    {
        var modelBuilder = default(ModelBuilder);

        Assert.Throws<ArgumentNullException>(nameof(modelBuilder),
            () => modelBuilder.OnHangfireModelCreating());
    }

    [Fact]
    public void OnHangfireModelCreating_ConfiguresModelCorectly()
    {
        var conventions = new ConventionSet();
        var modelBuilder = new ModelBuilder(conventions);

        modelBuilder.OnHangfireModelCreating();

        var model = modelBuilder.Model;
        Assert.NotNull(model.FindEntityType(typeof(HangfireCounter)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireHash)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireJob)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireJobParameter)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireList)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireLock)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireQueuedJob)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireSet)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireServer)));
        Assert.NotNull(model.FindEntityType(typeof(HangfireState)));
    }
}
