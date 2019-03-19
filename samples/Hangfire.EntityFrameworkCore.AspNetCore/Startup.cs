using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hangfire.EntityFrameworkCore.AspNetCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public static void ConfigureServices(IServiceCollection services)
        {
            var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var databaseFilePath = Path.Combine(userPath, "hangfire.db");
            services.AddHangfire(configuration =>
                configuration.UseEFCoreStorage(builder =>
                    builder.UseSqlite($"Data Source={databaseFilePath}"),
                    new EFCoreStorageOptions
                    {
                        CountersAggregationInterval = new TimeSpan(0, 5, 0),
                        DistributedLockTimeout = new TimeSpan(0, 10, 0),
                        JobExpirationCheckInterval = new TimeSpan(0, 30, 0),
                        QueuePollInterval = new TimeSpan(0, 0, 15),
                        Schema = "Hangfire",
                        SlidingInvisibilityTimeout = new TimeSpan(0, 5, 0),
                    }).
                UseDatabaseCreator());
            services.AddMvcCore();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseHangfireDashboard(string.Empty,
                new DashboardOptions
                {
                    AppPath = null,
                });
            app.UseHangfireServer();
            app.UseMvc();

            RecurringJob.AddOrUpdate(() => DoNothing(), Cron.Minutely);
        }

        public static void DoNothing()
        {
        }
    }
}
