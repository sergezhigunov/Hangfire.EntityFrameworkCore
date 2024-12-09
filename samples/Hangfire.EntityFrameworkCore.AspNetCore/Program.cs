using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.AspNetCore;

public static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration.GetConnectionString("HangfireConnection")
            ?? throw new InvalidOperationException("Connection string 'HangfireConnection' not found.");

        builder.Services.AddHangfire(configuration =>
            configuration.UseEFCoreStorage(builder =>
                builder.UseSqlite(connectionString),
                new EFCoreStorageOptions
                {
                    CountersAggregationInterval = new TimeSpan(0, 5, 0),
                    DistributedLockTimeout = new TimeSpan(0, 10, 0),
                    JobExpirationCheckInterval = new TimeSpan(0, 30, 0),
                    QueuePollInterval = new TimeSpan(0, 0, 15),
                    Schema = string.Empty,
                    SlidingInvisibilityTimeout = new TimeSpan(0, 5, 0),
                }).
            UseDatabaseCreator());

        builder.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = 1;
        });

        var app = builder.Build();

        app.UseHangfireDashboard(
            string.Empty,
            new DashboardOptions
            {
                AppPath = null,
                Authorization = [],
            });
        RecurringJob.AddOrUpdate(nameof(DoNothing), () => DoNothing(), Cron.Minutely);

        await app.RunAsync();
    }

    public static void DoNothing()
    {
    }
}
