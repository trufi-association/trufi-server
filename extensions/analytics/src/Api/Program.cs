using Microsoft.EntityFrameworkCore;
using Shared.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=postgres;Database=analytics;Username=analytics;Password=analytics";

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Analytics API", Version = "v1" });
});

var app = builder.Build();

var basePath = Environment.GetEnvironmentVariable("BASE_PATH") ?? "";

app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        if (!string.IsNullOrEmpty(basePath))
        {
            swaggerDoc.Servers = new List<Microsoft.OpenApi.Models.OpenApiServer>
            {
                new() { Url = basePath }
            };
        }
    });
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("v1/swagger.json", "Analytics API v1");
    c.RoutePrefix = "swagger";
});

// Wait for database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    for (int i = 0; i < 30; i++)
    {
        try
        {
            await db.Database.CanConnectAsync();
            Console.WriteLine("Database connected");
            break;
        }
        catch
        {
            Console.WriteLine($"Waiting for database... ({i + 1}/30)");
            await Task.Delay(1000);
        }
    }
}

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
})
.WithName("Health")
.WithTags("Health");

app.MapGet("/logs", async (
    AnalyticsDbContext db,
    DateTime? from,
    DateTime? to,
    string? host,
    string? method,
    string? deviceId,
    int limit = 100,
    int offset = 0) =>
{
    var query = db.Requests.AsQueryable();

    if (from.HasValue)
        query = query.Where(r => r.ReceivedAt >= from.Value);
    if (to.HasValue)
        query = query.Where(r => r.ReceivedAt <= to.Value);
    if (!string.IsNullOrEmpty(host))
        query = query.Where(r => r.Host == host);
    if (!string.IsNullOrEmpty(method))
        query = query.Where(r => r.Method == method);
    if (!string.IsNullOrEmpty(deviceId))
        query = query.Where(r => r.DeviceId == deviceId);

    var results = await query
        .OrderByDescending(r => r.ReceivedAt)
        .Skip(offset)
        .Take(limit)
        .Select(r => new
        {
            r.Id,
            r.Method,
            r.Uri,
            r.Host,
            r.Ip,
            r.DeviceId,
            r.UserAgent,
            r.Body,
            r.ReceivedAt
        })
        .ToListAsync();

    return Results.Ok(results);
})
.WithName("GetLogs")
.WithSummary("Get request logs with filters")
.WithTags("Logs");

app.MapGet("/stats", async (AnalyticsDbContext db, DateTime? from, DateTime? to, string? host) =>
{
    var query = db.Requests.AsQueryable();

    if (from.HasValue)
        query = query.Where(r => r.ReceivedAt >= from.Value);
    if (to.HasValue)
        query = query.Where(r => r.ReceivedAt <= to.Value);
    if (!string.IsNullOrEmpty(host))
        query = query.Where(r => r.Host == host);

    var totalRequests = await query.CountAsync();
    var uniqueDevices = await query.Where(r => r.DeviceId != null).Select(r => r.DeviceId).Distinct().CountAsync();
    var uniqueHosts = await query.Select(r => r.Host).Distinct().CountAsync();

    return Results.Ok(new
    {
        total_requests = totalRequests,
        unique_devices = uniqueDevices,
        unique_hosts = uniqueHosts
    });
})
.WithName("GetStats")
.WithSummary("Get general statistics")
.WithTags("Stats");

app.MapGet("/stats/hourly", async (AnalyticsDbContext db, DateTime? from, DateTime? to, string? host) =>
{
    var query = db.Requests.AsQueryable();

    if (from.HasValue)
        query = query.Where(r => r.ReceivedAt >= from.Value);
    if (to.HasValue)
        query = query.Where(r => r.ReceivedAt <= to.Value);
    if (!string.IsNullOrEmpty(host))
        query = query.Where(r => r.Host == host);

    var results = await query
        .GroupBy(r => new { Hour = r.ReceivedAt.Date.AddHours(r.ReceivedAt.Hour) })
        .Select(g => new { hour = g.Key.Hour, count = g.Count() })
        .OrderByDescending(x => x.hour)
        .Take(168) // Last 7 days
        .ToListAsync();

    return Results.Ok(results);
})
.WithName("GetHourlyStats")
.WithSummary("Get requests per hour")
.WithTags("Stats");

app.MapGet("/stats/endpoints", async (AnalyticsDbContext db, DateTime? from, DateTime? to, string? host, int limit = 20) =>
{
    var query = db.Requests.AsQueryable();

    if (from.HasValue)
        query = query.Where(r => r.ReceivedAt >= from.Value);
    if (to.HasValue)
        query = query.Where(r => r.ReceivedAt <= to.Value);
    if (!string.IsNullOrEmpty(host))
        query = query.Where(r => r.Host == host);

    var results = await query
        .GroupBy(r => r.Uri)
        .Select(g => new { uri = g.Key, count = g.Count() })
        .OrderByDescending(x => x.count)
        .Take(limit)
        .ToListAsync();

    return Results.Ok(results);
})
.WithName("GetTopEndpoints")
.WithSummary("Get top endpoints by request count")
.WithTags("Stats");

app.MapGet("/stats/devices", async (AnalyticsDbContext db, DateTime? from, DateTime? to, int limit = 20) =>
{
    var query = db.Requests.Where(r => r.DeviceId != null);

    if (from.HasValue)
        query = query.Where(r => r.ReceivedAt >= from.Value);
    if (to.HasValue)
        query = query.Where(r => r.ReceivedAt <= to.Value);

    var results = await query
        .GroupBy(r => r.DeviceId)
        .Select(g => new { device_id = g.Key, count = g.Count() })
        .OrderByDescending(x => x.count)
        .Take(limit)
        .ToListAsync();

    return Results.Ok(results);
})
.WithName("GetDeviceStats")
.WithSummary("Get requests by device")
.WithTags("Stats");

app.Run("http://0.0.0.0:3001");
