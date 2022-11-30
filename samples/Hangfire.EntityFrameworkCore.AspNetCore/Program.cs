namespace Hangfire.EntityFrameworkCore.AspNetCore;

internal static class Program
{
    private static async Task Main(string[] args)
        => await CreatebHostBuilder(args).Build().RunAsync();

    private static IHostBuilder CreatebHostBuilder(string[] args)
        => Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(builder => builder
                .UseStartup<Startup>());
}
