using Api.Services;

namespace Api.Controllers;

public static class StatsController
{
    public static void MapStatsController(this WebApplication app)
    {
        app.MapGet("/stats", GetGeneralStats)
            .WithName("GetStats")
            .WithSummary("Get general statistics")
            .WithTags("Stats");

        app.MapGet("/stats/hourly", GetHourlyStats)
            .WithName("GetHourlyStats")
            .WithSummary("Get requests per hour")
            .WithTags("Stats");

        app.MapGet("/stats/endpoints", GetTopEndpoints)
            .WithName("GetTopEndpoints")
            .WithSummary("Get top endpoints by request count")
            .WithTags("Stats");

        app.MapGet("/stats/devices", GetDeviceStats)
            .WithName("GetDeviceStats")
            .WithSummary("Get requests by device")
            .WithTags("Stats");
    }

    private static async Task<IResult> GetGeneralStats(
        IStatsService statsService,
        DateTime? from,
        DateTime? to,
        string? host)
    {
        var filter = new StatsFilter(from, to, host);
        var stats = await statsService.GetGeneralStatsAsync(filter);

        return Results.Ok(new
        {
            total_requests = stats.TotalRequests,
            unique_devices = stats.UniqueDevices,
            unique_hosts = stats.UniqueHosts
        });
    }

    private static async Task<IResult> GetHourlyStats(
        IStatsService statsService,
        DateTime? from,
        DateTime? to,
        string? host)
    {
        var filter = new StatsFilter(from, to, host);
        var results = await statsService.GetHourlyStatsAsync(filter);

        return Results.Ok(results.Select(r => new { hour = r.Hour, count = r.Count }));
    }

    private static async Task<IResult> GetTopEndpoints(
        IStatsService statsService,
        DateTime? from,
        DateTime? to,
        string? host,
        int limit = 20)
    {
        var filter = new StatsFilter(from, to, host);
        var results = await statsService.GetTopEndpointsAsync(filter, limit);

        return Results.Ok(results.Select(r => new { uri = r.Uri, count = r.Count }));
    }

    private static async Task<IResult> GetDeviceStats(
        IStatsService statsService,
        DateTime? from,
        DateTime? to,
        int limit = 20)
    {
        var filter = new StatsFilter(from, to, null);
        var results = await statsService.GetDeviceStatsAsync(filter, limit);

        return Results.Ok(results.Select(r => new { device_id = r.DeviceId, count = r.Count }));
    }
}
