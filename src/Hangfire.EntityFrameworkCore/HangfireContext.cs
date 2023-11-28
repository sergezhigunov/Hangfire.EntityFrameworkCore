using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NotNullAttribute = Hangfire.Annotations.NotNullAttribute;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireContext : DbContext
{
    internal string Schema { get; }

    [SuppressMessage("Maintainability", "CA1510")]
    public HangfireContext([NotNull] DbContextOptions options, [NotNull] string schema)
        : base(options)
    {
        if (schema is null)
            throw new ArgumentNullException(nameof(schema));

        Schema = schema;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, HangfireModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (!string.IsNullOrEmpty(Schema))
            modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.OnHangfireModelCreating();
    }
}
