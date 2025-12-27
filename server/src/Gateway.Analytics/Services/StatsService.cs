using Gateway.Shared.Data;
using Gateway.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Analytics.Services;

public class StatsService : IStatsService
{
    private readonly AnalyticsDbContext _context;

    public StatsService(AnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<GeneralStats> GetGeneralStatsAsync(StatsFilter filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_context.Requests.AsQueryable(), filter);

        var totalRequests = await query.CountAsync(cancellationToken);
        var uniqueDevices = await query
            .Where(r => r.DeviceId != null)
            .Select(r => r.DeviceId)
            .Distinct()
            .CountAsync(cancellationToken);
        var uniqueHosts = await query
            .Select(r => r.Host)
            .Distinct()
            .CountAsync(cancellationToken);

        return new GeneralStats(totalRequests, uniqueDevices, uniqueHosts);
    }

    public async Task<IReadOnlyList<HourlyStats>> GetHourlyStatsAsync(StatsFilter filter, int maxHours = 168, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_context.Requests.AsQueryable(), filter);

        var data = await query
            .Select(r => r.ReceivedAt)
            .ToListAsync(cancellationToken);

        var results = data
            .GroupBy(d => new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0, d.Kind))
            .Select(g => new HourlyStats(g.Key, g.Count()))
            .OrderByDescending(x => x.Hour)
            .Take(maxHours)
            .ToList();

        return results;
    }

    public async Task<IReadOnlyList<EndpointStats>> GetTopEndpointsAsync(StatsFilter filter, int limit = 20, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_context.Requests.AsQueryable(), filter);

        var data = await query
            .Select(r => r.Uri)
            .ToListAsync(cancellationToken);

        var results = data
            .GroupBy(u => u)
            .Select(g => new EndpointStats(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToList();

        return results;
    }

    public async Task<IReadOnlyList<DeviceStats>> GetDeviceStatsAsync(StatsFilter filter, int limit = 20, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_context.Requests.Where(r => r.DeviceId != null), filter);

        var data = await query
            .Select(r => r.DeviceId!)
            .ToListAsync(cancellationToken);

        var results = data
            .GroupBy(d => d)
            .Select(g => new DeviceStats(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToList();

        return results;
    }

    private static IQueryable<Request> ApplyFilter(IQueryable<Request> query, StatsFilter filter)
    {
        if (filter.From.HasValue)
            query = query.Where(r => r.ReceivedAt >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(r => r.ReceivedAt <= filter.To.Value);
        if (!string.IsNullOrEmpty(filter.Host))
            query = query.Where(r => r.Host == filter.Host);

        return query;
    }
}
