using Collector.Controllers;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=postgres;Database=analytics;Username=analytics;Password=analytics";

builder.Services.AddAnalyticsServices(connectionString);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.MapHealthController();
app.MapLogController();

app.Run("http://0.0.0.0:3000");

namespace Collector
{
    public partial class Program { }
}
