using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext;

public static class Program
{
    private static async Task Main(string[] args)
    {
        AppContext.SetData("DataDirectory", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration.GetConnectionString("HangfireConnection")
            ?? throw new InvalidOperationException("Connection string 'HangfireConnection' not found.");

        builder.Services.AddDbContextFactory<SampleDbContext>(builder => builder.UseSqlite(connectionString));
        builder.Services.AddHangfire((serviceProvider, configuration) =>
            configuration.UseEFCoreStorage(
                () => serviceProvider.GetRequiredService<IDbContextFactory<SampleDbContext>>().CreateDbContext(),
                new EFCoreStorageOptions
                {
                    CountersAggregationInterval = new TimeSpan(0, 5, 0),
                    DistributedLockTimeout = new TimeSpan(0, 10, 0),
                    JobExpirationCheckInterval = new TimeSpan(0, 30, 0),
                    QueuePollInterval = new TimeSpan(0, 0, 15),
                    Schema = string.Empty,
                    SlidingInvisibilityTimeout = new TimeSpan(0, 5, 0),
                }));

        builder.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = 1;
        });

        var app = builder.Build();
        using (var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await context.Database.MigrateAsync();
        }
        app.UseHangfireDashboard(
            string.Empty,
            new DashboardOptions
            {
                AppPath = null,
                Authorization = [],
            });
        RecurringJob.AddOrUpdate(nameof(HelloWorld), () => HelloWorld(), Cron.Minutely);

        await app.RunAsync();
    }

    public static void HelloWorld()
    {
        Console.WriteLine("hello world");
    }
}
