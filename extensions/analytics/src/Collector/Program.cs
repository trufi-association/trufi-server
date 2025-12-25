using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=postgres;Database=analytics;Username=analytics;Password=analytics";

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

    for (int i = 0; i < 30; i++)
    {
        try
        {
            await db.Database.MigrateAsync();
            Console.WriteLine("Database migrated successfully");
            break;
        }
        catch
        {
            Console.WriteLine($"Waiting for database... ({i + 1}/30)");
            await Task.Delay(1000);
        }
    }
}

app.MapPost("/log", async (HttpContext context, AnalyticsDbContext db) =>
{
    var headers = context.Request.Headers;

    string body;
    using (var reader = new StreamReader(context.Request.Body))
    {
        body = await reader.ReadToEndAsync();
    }

    var request = new Request
    {
        Method = headers["X-Original-Method"].FirstOrDefault() ?? "",
        Uri = headers["X-Original-URI"].FirstOrDefault() ?? "",
        Host = headers["X-Original-Host"].FirstOrDefault() ?? "",
        Ip = headers["X-Real-IP"].FirstOrDefault(),
        DeviceId = headers["X-Device-Id"].FirstOrDefault() ?? headers["Device-Id"].FirstOrDefault(),
        UserAgent = headers["User-Agent"].FirstOrDefault(),
        Body = body,
        ReceivedAt = DateTime.UtcNow
    };

    db.Requests.Add(request);
    await db.SaveChangesAsync();

    return Results.Ok();
});

app.MapGet("/health", async (AnalyticsDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

app.Run("http://0.0.0.0:3000");
