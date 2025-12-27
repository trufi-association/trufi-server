using Shared.Models;

namespace Shared.Services;

public interface IRequestService
{
    Task<Request> CreateRequestAsync(CreateRequestDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Request>> GetRequestsAsync(RequestFilter filter, CancellationToken cancellationToken = default);
    Task<Request?> GetRequestByIdAsync(long id, CancellationToken cancellationToken = default);
}

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
