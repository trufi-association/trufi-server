using Shared.Services;

namespace Api.Controllers;

public static class LogsController
{
    public static void MapLogsController(this WebApplication app)
    {
        app.MapGet("/logs", async (
            IRequestService requestService,
            DateTime? from,
            DateTime? to,
            string? host,
            string? method,
            string? deviceId,
            int limit = 100,
            int offset = 0) =>
        {
            var filter = new RequestFilter(from, to, host, method, deviceId, limit, offset);
            var results = await requestService.GetRequestsAsync(filter);

            return Results.Ok(results.Select(r => new
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
        })
        .WithName("GetLogs")
        .WithSummary("Get request logs with filters")
        .WithTags("Logs");
    }
}
