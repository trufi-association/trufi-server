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
            RequestContentType = dto.RequestContentType,
            RequestHeaders = dto.RequestHeaders,
            Body = dto.Body,
            StatusCode = dto.StatusCode,
            ResponseContentType = dto.ResponseContentType,
            ResponseHeaders = dto.ResponseHeaders,
            ResponseBody = dto.ResponseBody,
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
        if (filter.StatusCode.HasValue)
            query = query.Where(r => r.StatusCode == filter.StatusCode.Value);
        if (!string.IsNullOrEmpty(filter.Ip))
            query = query.Where(r => r.Ip == filter.Ip);
        if (!string.IsNullOrEmpty(filter.UriContains))
            query = query.Where(r => r.Uri.Contains(filter.UriContains));
        if (!string.IsNullOrEmpty(filter.HeaderContains))
            query = query.Where(r =>
                (r.RequestHeaders != null && r.RequestHeaders.Contains(filter.HeaderContains)) ||
                (r.ResponseHeaders != null && r.ResponseHeaders.Contains(filter.HeaderContains)));
        if (!string.IsNullOrEmpty(filter.BodyContains))
            query = query.Where(r =>
                (r.Body != null && r.Body.Contains(filter.BodyContains)) ||
                (r.ResponseBody != null && r.ResponseBody.Contains(filter.BodyContains)));
        if (!string.IsNullOrEmpty(filter.Search))
            query = query.Where(r =>
                r.Uri.Contains(filter.Search) ||
                (r.RequestHeaders != null && r.RequestHeaders.Contains(filter.Search)) ||
                (r.ResponseHeaders != null && r.ResponseHeaders.Contains(filter.Search)) ||
                (r.Body != null && r.Body.Contains(filter.Search)) ||
                (r.ResponseBody != null && r.ResponseBody.Contains(filter.Search)));

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
