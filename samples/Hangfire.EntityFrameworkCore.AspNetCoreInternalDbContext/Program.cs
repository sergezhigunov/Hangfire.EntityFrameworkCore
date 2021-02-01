using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Hangfire.EntityFrameworkCore.AspNetCoreInternalDbContext
{
    internal static class Program
    {
        private static async Task Main(string[] args)
            => await CreateWebHostBuilder(args).Build().RunAsync();

        private static IHostBuilder CreateWebHostBuilder(string[] args)
            => Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(builder => builder
                    .UseStartup<Startup>());
    }
}
