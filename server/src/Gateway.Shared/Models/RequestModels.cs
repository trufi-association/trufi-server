namespace Gateway.Shared.Models;

public record CreateRequestDto(
    string Method,
    string Uri,
    string Host,
    string? Ip,
    string? DeviceId,
    string? UserAgent,
    string? RequestContentType,
    string? RequestHeaders,
    string? Body,
    int? StatusCode = null,
    string? ResponseContentType = null,
    string? ResponseHeaders = null,
    string? ResponseBody = null
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
