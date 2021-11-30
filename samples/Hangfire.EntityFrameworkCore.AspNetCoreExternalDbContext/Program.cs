using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hangfire.EntityFrameworkCore.AspNetCoreExternalDbContext;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        using (var scope = host.Services.CreateScope())
        {
            using var context = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<SampleDbContext>>()
                .CreateDbContext();
            await context.Database.MigrateAsync();
        }
        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
        => Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(builder => builder
                .UseStartup<Startup>());
}
