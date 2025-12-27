using Microsoft.AspNetCore.Mvc;

namespace Gateway.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy" });
}
