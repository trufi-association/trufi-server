using Shared.Services;

namespace Api.Controllers;

public static class HealthController
{
    public static void MapHealthController(this WebApplication app)
    {
        app.MapGet("/health", async (IHealthService healthService) =>
        {
            try
            {
                var isHealthy = await healthService.CheckDatabaseConnectionAsync();
                return isHealthy
                    ? Results.Ok(new { status = "ok" })
                    : Results.StatusCode(503);
            }
            catch
            {
                return Results.StatusCode(503);
            }
        })
        .WithName("Health")
        .WithTags("Health");
    }
}
