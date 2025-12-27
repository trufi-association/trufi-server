using Gateway.Shared.Data;
using Gateway.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Shared.Services;

public class RequestService : IRequestService
{
    private readonly AnalyticsDbContext _context;

    public RequestService(AnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<Request> CreateRequestAsync(CreateRequestDto dto, CancellationToken cancellationToken = default)
    {
        var request = new Request
        {
            Method = dto.Method,
            Uri = dto.Uri,
            Host = dto.Host,
            Ip = dto.Ip,
            DeviceId = dto.DeviceId,
            UserAgent = dto.UserAgent,
            Body = dto.Body,
            ReceivedAt = DateTime.UtcNow
        };

        _context.Requests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<IReadOnlyList<Request>> GetRequestsAsync(RequestFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Requests.AsQueryable();

        if (filter.From.HasValue)
            query = query.Where(r => r.ReceivedAt >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(r => r.ReceivedAt <= filter.To.Value);
        if (!string.IsNullOrEmpty(filter.Host))
            query = query.Where(r => r.Host == filter.Host);
        if (!string.IsNullOrEmpty(filter.Method))
            query = query.Where(r => r.Method == filter.Method);
        if (!string.IsNullOrEmpty(filter.DeviceId))
            query = query.Where(r => r.DeviceId == filter.DeviceId);

        return await query
            .OrderByDescending(r => r.ReceivedAt)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Request?> GetRequestByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.Requests.FindAsync(new object[] { id }, cancellationToken);
    }
}
