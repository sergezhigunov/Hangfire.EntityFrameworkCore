using System.Collections.Generic;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireContext : DbContext
    {
        private readonly string _defaultSchema;

        public HangfireContext([NotNull] DbContextOptions options, string defaultSchema) :
            base(options)
        {
            _defaultSchema = defaultSchema;
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            if (_defaultSchema != null)
                modelBuilder.HasDefaultSchema(_defaultSchema);

            modelBuilder.Entity<HangfireCounter>(entity =>
            {
                entity.HasIndex(x => new { x.Key, x.Value });
                entity.HasIndex(x => x.ExpireAt);
            });

            modelBuilder.Entity<HangfireHash>(entity =>
            {
                entity.HasKey(x => new { x.Key, x.Field });
                entity.HasIndex(x => x.ExpireAt);
            });

            modelBuilder.Entity<HangfireJob>(entity =>
            {
                entity.HasOne(x => x.ActualState).
                    WithOne(x => x.Job).
                    HasForeignKey<HangfireJobState>(x => x.JobId).
                    OnDelete(DeleteBehavior.Restrict);
                entity.Property(x => x.InvocationData).HasConversion(
                    x => JobHelper.ToJson(x),
                    x => JobHelper.FromJson<InvocationData>(x));
                entity.HasIndex(x => x.ExpireAt);
            });

            modelBuilder.Entity<HangfireJobParameter>(entity =>
            {
                entity.HasKey(x => new { x.JobId, x.Name });
            });

            modelBuilder.Entity<HangfireJobState>(entity =>
            {
                entity.HasKey(x => x.JobId);
                entity.HasIndex(x => x.Name);
            });

            modelBuilder.Entity<HangfireList>(entity =>
            {
                entity.HasKey(x => new { x.Key, x.Position });
                entity.HasIndex(x => x.ExpireAt);
            });

            modelBuilder.Entity<HangfireLock>();

            modelBuilder.Entity<HangfireQueuedJob>(entity =>
            {
                entity.HasIndex(x => new { x.Queue, x.FetchedAt });
            });

            modelBuilder.Entity<HangfireSet>(entity =>
            {
                entity.HasKey(x => new { x.Key, x.Value });
                entity.HasIndex(x => new { x.Key, x.Score });
                entity.HasIndex(x => x.ExpireAt);
            });

            modelBuilder.Entity<HangfireServer>(entity =>
            {
                entity.Property(x => x.Queues).HasConversion(
                    x => JobHelper.ToJson(x),
                    x => JobHelper.FromJson<string[]>(x));
                entity.HasIndex(x => x.Heartbeat);
            });

            modelBuilder.Entity<HangfireState>(entity =>
            {
                entity.HasIndex(x => x.JobId);
                entity.Property(x => x.Data).HasConversion(
                    x => JobHelper.ToJson(x),
                    x => JobHelper.FromJson<Dictionary<string, string>>(x));
            });
        }
    }
}
