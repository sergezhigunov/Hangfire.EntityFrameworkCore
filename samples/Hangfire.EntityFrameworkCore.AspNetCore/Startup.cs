using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkCore.AspNetCore;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        var connectionString = Configuration.GetConnectionString("HangfireConnection");
        services.AddHangfire(configuration =>
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
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 1;
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();

        app.UseHangfireDashboard(string.Empty,
            new DashboardOptions
            {
                AppPath = null,
                Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
            });
        RecurringJob.AddOrUpdate(() => DoNothing(), Cron.Minutely);
    }

    public static void DoNothing()
    {
    }
}
