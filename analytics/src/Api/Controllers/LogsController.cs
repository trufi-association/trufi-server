using Microsoft.AspNetCore.Mvc;
using Shared.Services;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class LogsController : ControllerBase
{
    private readonly IRequestService _requestService;

    public LogsController(IRequestService requestService)
    {
        _requestService = requestService;
    }

    [HttpGet("/logs")]
    public async Task<IActionResult> GetLogs(
        DateTime? from,
        DateTime? to,
        string? host,
        string? method,
        string? deviceId,
        int limit = 100,
        int offset = 0)
    {
        var filter = new RequestFilter(from, to, host, method, deviceId, limit, offset);
        var results = await _requestService.GetRequestsAsync(filter);

        return Ok(results.Select(r => new
        {
            r.Id,
            r.Method,
            r.Uri,
            r.Host,
            r.Ip,
            r.DeviceId,
            r.UserAgent,
            r.Body,
            r.ReceivedAt
        }));
    }
}
