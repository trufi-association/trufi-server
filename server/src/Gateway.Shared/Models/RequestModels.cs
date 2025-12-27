namespace Gateway.Shared.Models;

public record CreateRequestDto(
    string Method,
    string Uri,
    string Host,
    string? Ip,
    string? DeviceId,
    string? UserAgent,
    string? Body
);

public record RequestFilter(
    DateTime? From = null,
    DateTime? To = null,
    string? Host = null,
    string? Method = null,
    string? DeviceId = null,
    int Limit = 100,
    int Offset = 0
);
