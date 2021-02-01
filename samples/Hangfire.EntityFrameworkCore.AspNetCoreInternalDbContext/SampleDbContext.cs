using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext
{
    public class SampleDbContext : DbContext
    {
        public DbSet<SampleTable> SampleTables { get; set; }

        public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.OnHangfireModelCreating();
        }
    }
}
