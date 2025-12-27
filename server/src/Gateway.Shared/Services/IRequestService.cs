using Gateway.Shared.Models;

namespace Gateway.Shared.Services;

public interface IRequestService
{
    Task<Request> CreateRequestAsync(CreateRequestDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Request>> GetRequestsAsync(RequestFilter filter, CancellationToken cancellationToken = default);
    Task<Request?> GetRequestByIdAsync(long id, CancellationToken cancellationToken = default);
}
