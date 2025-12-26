using Shared.Services;

namespace Collector.Controllers;

public static class LogController
{
    public static void MapLogController(this WebApplication app)
    {
        app.MapPost("/log", async (HttpContext context, IRequestService requestService) =>
        {
            var headers = context.Request.Headers;

            string body;
            using (var reader = new StreamReader(context.Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }

            var dto = new CreateRequestDto(
                Method: headers["X-Original-Method"].FirstOrDefault() ?? "",
                Uri: headers["X-Original-URI"].FirstOrDefault() ?? "",
                Host: headers["X-Original-Host"].FirstOrDefault() ?? "",
                Ip: headers["X-Real-IP"].FirstOrDefault(),
                DeviceId: headers["X-Device-Id"].FirstOrDefault() ?? headers["Device-Id"].FirstOrDefault(),
                UserAgent: headers["User-Agent"].FirstOrDefault(),
                Body: body
            );

            await requestService.CreateRequestAsync(dto);

            return Results.Ok();
        });
    }
}
