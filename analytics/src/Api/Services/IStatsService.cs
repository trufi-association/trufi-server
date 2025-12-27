namespace Api.Services;

public interface IStatsService
{
    Task<GeneralStats> GetGeneralStatsAsync(StatsFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HourlyStats>> GetHourlyStatsAsync(StatsFilter filter, int maxHours = 168, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EndpointStats>> GetTopEndpointsAsync(StatsFilter filter, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceStats>> GetDeviceStatsAsync(StatsFilter filter, int limit = 20, CancellationToken cancellationToken = default);
}

public record StatsFilter(
    DateTime? From = null,
    DateTime? To = null,
    string? Host = null
);

public record GeneralStats(
    int TotalRequests,
    int UniqueDevices,
    int UniqueHosts
);

public record HourlyStats(
    DateTime Hour,
    int Count
);

public record EndpointStats(
    string Uri,
    int Count
);

public record DeviceStats(
    string DeviceId,
    int Count
);
