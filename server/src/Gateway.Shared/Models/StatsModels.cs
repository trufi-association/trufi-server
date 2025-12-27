namespace Gateway.Shared.Models;

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
