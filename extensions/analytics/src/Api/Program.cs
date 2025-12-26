using Api.Configuration;
using Api.Controllers;
using Api.Services;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=postgres;Database=analytics;Username=analytics;Password=analytics";

builder.Services.AddAnalyticsServices(connectionString);
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddSwaggerConfiguration();

var app = builder.Build();

app.UseSwaggerConfiguration();

if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.WaitForDatabaseAsync();
}

app.MapHealthController();
app.MapLogsController();
app.MapStatsController();

app.Run("http://0.0.0.0:3001");

namespace Api
{
    public partial class Program { }
}
