using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Public extensions for a DbContext model builder
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configures the Hangfire EF Core model.
    /// </summary>
    /// <param name="modelBuilder">
    /// The model builder to configure with.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modelBuilder"/> is <see langword="null"/>.
    /// </exception>
    [CLSCompliant(false)]
    [SuppressMessage("Maintainability", "CA1510")]
    public static void OnHangfireModelCreating(
        this ModelBuilder modelBuilder)
        => modelBuilder.OnHangfireModelCreating(string.Empty);

    /// <summary>
    /// Configures the Hangfire EF Core model.
    /// </summary>
    /// <param name="modelBuilder">
    /// The model builder to configure with.
    /// </param>
    /// <param name="schema">
    /// Gets or set DB storage schema name. The <see cref="string.Empty"/> value means that
    /// the provider-specific default schema name will be used.
    /// The default value is <see cref="string.Empty"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modelBuilder"/> is <see langword="null"/>.
    /// </exception>
    [CLSCompliant(false)]
    [SuppressMessage("Maintainability", "CA1510")]
    public static void OnHangfireModelCreating(
        this ModelBuilder modelBuilder,
        string schema)
    {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(modelBuilder);
#else
        if (modelBuilder is null) throw new ArgumentNullException(nameof(modelBuilder));
#endif
        void SetSchema(EntityTypeBuilder entity)
        {
            if (!string.IsNullOrEmpty(schema))
                entity.Metadata.SetSchema(schema);
        }
        modelBuilder.Entity<HangfireCounter>(entity =>
        {
            entity.HasIndex(x => new { x.Key, x.Value, });
            entity.HasIndex(x => x.ExpireAt);
            SetSchema(entity);
        });
        modelBuilder.Entity<HangfireHash>(entity =>
        {
            entity.HasKey(x => new { x.Key, x.Field });
            entity.HasIndex(x => x.ExpireAt);
            SetSchema(entity);
        });
        modelBuilder.Entity<HangfireJob>(entity =>
        {
            entity.Property(x => x.InvocationData)
                .HasConversion(
                    x => SerializationHelper.Serialize(x),
                    x => SerializationHelper.Deserialize<InvocationData>(x));
            entity.HasIndex(x => x.StateName);
            entity.HasIndex(x => x.ExpireAt);
            SetSchema(entity);
        });
        modelBuilder.Entity<HangfireJobParameter>(entity =>
        {
            entity.HasKey(x => new { x.JobId, x.Name });
            SetSchema(entity);
        });
        modelBuilder.Entity<HangfireList>(entity =>
        {
            entity.HasKey(x => new { x.Key, x.Position });
            entity.HasIndex(x => x.ExpireAt);
            SetSchema(entity);
        });

        modelBuilder.Entity<HangfireLock>(SetSchema);
        modelBuilder.Entity<HangfireQueuedJob>(entity =>
        {
            entity.HasIndex(nameof(HangfireQueuedJob.Queue), nameof(HangfireQueuedJob.FetchedAt));
            SetSchema(entity);
        });
        modelBuilder.Entity<HangfireSet>(entity =>
        {
            entity.HasKey(x => new { x.Key, x.Value });
            entity.HasIndex(x => new { x.Key, x.Score, });
            entity.HasIndex(x => x.ExpireAt);
            SetSchema(entity);
        });
        modelBuilder.Entity<HangfireServer>(entity =>
        {
            entity.Property(x => x.Queues)
                .HasConversion(
                    x => SerializationHelper.Serialize(x),
                    x => SerializationHelper.Deserialize<string[]>(x));
            entity.HasIndex(x => x.Heartbeat);
            SetSchema(entity);
        });
        modelBuilder.Entity<HangfireState>(entity =>
        {
            entity.Property(x => x.Data)
               .HasConversion(
                   x => SerializationHelper.Serialize(x),
                   x => SerializationHelper.Deserialize<Dictionary<string, string>>(x));
            entity.HasIndex(x => x.JobId);
            entity.HasMany<HangfireJob>().
                WithOne(x => x.State).
                HasForeignKey(x => x.StateId);
            SetSchema(entity);
        });
    }
}
