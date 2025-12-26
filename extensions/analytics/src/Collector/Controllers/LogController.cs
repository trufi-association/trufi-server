using Microsoft.AspNetCore.Mvc;
using Shared.Services;

namespace Collector.Controllers;

[ApiController]
[Route("[controller]")]
public class LogController : ControllerBase
{
    private readonly IRequestService _requestService;

    public LogController(IRequestService requestService)
    {
        _requestService = requestService;
    }

    [HttpPost("/log")]
    public async Task<IActionResult> PostLog()
    {
        var headers = Request.Headers;

        string body;
        using (var reader = new StreamReader(Request.Body))
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

        await _requestService.CreateRequestAsync(dto);

        return Ok();
    }
}
