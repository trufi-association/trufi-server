using Shared.Extensions;

namespace Collector;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=postgres;Database=analytics;Username=analytics;Password=analytics";

        builder.Services.AddControllers();
        builder.Services.AddAnalyticsServices(connectionString);

        var app = builder.Build();

        if (!app.Environment.IsEnvironment("Testing"))
        {
            await app.Services.MigrateDatabaseAsync();
        }

        app.MapControllers();

        app.Run();
    }
}
