using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext;

public class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<SampleTable> SampleTables { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.OnHangfireModelCreating();
    }
}
