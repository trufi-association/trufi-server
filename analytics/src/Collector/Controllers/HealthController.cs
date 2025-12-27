using Microsoft.AspNetCore.Mvc;
using Shared.Services;

namespace Collector.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet("/health")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var isHealthy = await _healthService.CheckDatabaseConnectionAsync();
            return isHealthy
                ? Ok(new { status = "ok" })
                : StatusCode(503);
        }
        catch
        {
            return StatusCode(503);
        }
    }
}
