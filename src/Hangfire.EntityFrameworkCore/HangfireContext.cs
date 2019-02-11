using System.Collections.Generic;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireContext : DbContext
    {
        public HangfireContext([NotNull] DbContextOptions options) :
            base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var counterBuilder = modelBuilder.Entity<HangfireCounter>();
            counterBuilder.HasIndex(x => new { x.Key, x.Value });

            var hashBuilder = modelBuilder.Entity<HangfireHash>();
            hashBuilder.HasKey(x => new { x.Key, x.Field });

            var jobBuilder = modelBuilder.Entity<HangfireJob>();
            jobBuilder.HasOne(x => x.ActualState).
                WithOne(x => x.Job).
                HasForeignKey<HangfireJobState>(x => x.JobId);
            jobBuilder.Property(x => x.InvocationData).HasConversion(
                x => JobHelper.ToJson(x),
                x => JobHelper.FromJson<InvocationData>(x));

            var jobParameterBuilder = modelBuilder.Entity<HangfireJobParameter>();
            jobParameterBuilder.HasKey(x => new { x.JobId, x.Name });

            var jobQueueBuilder = modelBuilder.Entity<HangfireJobQueue>();
            jobQueueBuilder.HasIndex(x => new { x.Queue, x.FetchedAt });

            var jobStateBuilder = modelBuilder.Entity<HangfireJobState>();
            jobStateBuilder.HasKey(x => x.JobId);
            jobStateBuilder.HasIndex(x => x.Name);

            var listBuilder = modelBuilder.Entity<HangfireList>();
            listBuilder.HasKey(x => new { x.Key, x.Position });

            modelBuilder.Entity<HangfireLock>();

            var setBuilder = modelBuilder.Entity<HangfireSet>();
            setBuilder.HasKey(x => new { x.Key, x.Value });

            var serverBuilder = modelBuilder.Entity<HangfireServer>();
            serverBuilder.Property(x => x.Queues).HasConversion(
                x => JobHelper.ToJson(x),
                x => JobHelper.FromJson<string[]>(x));

            var stateBuilder = modelBuilder.Entity<HangfireState>();
            stateBuilder.HasIndex(x => x.JobId);
            stateBuilder.Property(x => x.Data).HasConversion(
                x => JobHelper.ToJson(x),
                x => JobHelper.FromJson<Dictionary<string, string>>(x));
        }
    }
}
