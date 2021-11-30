using System;
using Microsoft.EntityFrameworkCore;

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
    public static void OnHangfireModelCreating(this ModelBuilder modelBuilder)
    {
        if (modelBuilder is null)
            throw new ArgumentNullException(nameof(modelBuilder));

        modelBuilder.Entity<HangfireCounter>(entity =>
        {
            entity.HasIndex(nameof(HangfireCounter.Key), nameof(HangfireCounter.Value));
            entity.HasIndex(nameof(HangfireCounter.ExpireAt));
        });

        modelBuilder.Entity<HangfireHash>(entity =>
        {
            entity.HasKey(x => new { x.Key, x.Field });
            entity.HasIndex(nameof(HangfireHash.ExpireAt));
        });

        modelBuilder.Entity<HangfireJob>(entity =>
        {
            entity.HasIndex(nameof(HangfireJob.StateName));
            entity.HasIndex(nameof(HangfireJob.ExpireAt));
        });

        modelBuilder.Entity<HangfireJobParameter>(entity =>
        {
            entity.HasKey(x => new { x.JobId, x.Name });
        });

        modelBuilder.Entity<HangfireList>(entity =>
        {
            entity.HasKey(x => new { x.Key, x.Position });
            entity.HasIndex(nameof(HangfireList.ExpireAt));
        });

        modelBuilder.Entity<HangfireLock>();

        modelBuilder.Entity<HangfireQueuedJob>(entity =>
        {
            entity.HasIndex(nameof(HangfireQueuedJob.Queue), nameof(HangfireQueuedJob.FetchedAt));
        });

        modelBuilder.Entity<HangfireSet>(entity =>
        {
            entity.HasKey(x => new { x.Key, x.Value });
            entity.HasIndex(nameof(HangfireSet.Key), nameof(HangfireSet.Score));
            entity.HasIndex(nameof(HangfireSet.ExpireAt));
        });

        modelBuilder.Entity<HangfireServer>(entity =>
        {
            entity.HasIndex(nameof(HangfireServer.Heartbeat));
        });

        modelBuilder.Entity<HangfireState>(entity =>
        {
            entity.HasIndex(nameof(HangfireState.JobId));
            entity.HasMany<HangfireJob>().
                WithOne(x => x.State).
                HasForeignKey(x => x.StateId);
        });
    }
}
