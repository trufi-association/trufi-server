using Api.Configuration;
using Api.Services;
using Shared.Extensions;

namespace Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=postgres;Database=analytics;Username=analytics;Password=analytics";

        builder.Services.AddControllers();
        builder.Services.AddAnalyticsServices(connectionString);
        builder.Services.AddScoped<IStatsService, StatsService>();
        builder.Services.AddSwaggerConfiguration();

        var app = builder.Build();

        app.UseSwaggerConfiguration();

        if (!app.Environment.IsEnvironment("Testing"))
        {
            await app.Services.WaitForDatabaseAsync();
        }

        app.MapControllers();

        app.Run();
    }
}
