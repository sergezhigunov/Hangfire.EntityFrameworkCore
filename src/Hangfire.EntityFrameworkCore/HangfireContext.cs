using System;
using Hangfire.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireContext : DbContext
    {
        internal string Schema { get; }

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
}
