using Gateway.Analytics.Services;
using Gateway.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Analytics.Controllers;

[ApiController]
[Route("analytics-api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetStats(DateTime? from, DateTime? to, string? host)
    {
        var filter = new StatsFilter(from, to, host);
        var stats = await _statsService.GetGeneralStatsAsync(filter);

        return Ok(new
        {
            total_requests = stats.TotalRequests,
            unique_devices = stats.UniqueDevices,
            unique_hosts = stats.UniqueHosts
        });
    }

    [HttpGet("hourly")]
    public async Task<IActionResult> GetHourlyStats(DateTime? from, DateTime? to, string? host)
    {
        var filter = new StatsFilter(from, to, host);
        var results = await _statsService.GetHourlyStatsAsync(filter);

        return Ok(results.Select(r => new { hour = r.Hour, count = r.Count }));
    }

    [HttpGet("endpoints")]
    public async Task<IActionResult> GetTopEndpoints(DateTime? from, DateTime? to, string? host, int limit = 20)
    {
        var filter = new StatsFilter(from, to, host);
        var results = await _statsService.GetTopEndpointsAsync(filter, limit);

        return Ok(results.Select(r => new { uri = r.Uri, count = r.Count }));
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDeviceStats(DateTime? from, DateTime? to, int limit = 20)
    {
        var filter = new StatsFilter(from, to, null);
        var results = await _statsService.GetDeviceStatsAsync(filter, limit);

        return Ok(results.Select(r => new { device_id = r.DeviceId, count = r.Count }));
    }
}
