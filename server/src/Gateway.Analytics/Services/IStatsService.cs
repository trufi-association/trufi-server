using Gateway.Shared.Models;

namespace Gateway.Analytics.Services;

public interface IStatsService
{
    Task<GeneralStats> GetGeneralStatsAsync(StatsFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HourlyStats>> GetHourlyStatsAsync(StatsFilter filter, int maxHours = 168, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EndpointStats>> GetTopEndpointsAsync(StatsFilter filter, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceStats>> GetDeviceStatsAsync(StatsFilter filter, int limit = 20, CancellationToken cancellationToken = default);
}
