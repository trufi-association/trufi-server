using Gateway.Shared.Models;
using Gateway.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Gateway.Middleware;

public class AnalyticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AnalyticsMiddleware> _logger;

    // Paths to exclude from analytics
    private static readonly string[] ExcludedPaths = ["/health", "/analytics-api"];

    public AnalyticsMiddleware(RequestDelegate next, ILogger<AnalyticsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRequestService requestService)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip analytics for excluded paths
        if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Read request body if present
        string? body = null;
        if (context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Capture all data BEFORE the request completes (context will be disposed after)
        var dto = new CreateRequestDto(
            Method: context.Request.Method,
            Uri: context.Request.Path + context.Request.QueryString,
            Host: context.Request.Host.Value ?? "unknown",
            Ip: context.Connection.RemoteIpAddress?.ToString(),
            DeviceId: context.Request.Headers["X-Device-Id"].FirstOrDefault()
                   ?? context.Request.Headers["Device-Id"].FirstOrDefault(),
            UserAgent: context.Request.Headers.UserAgent.FirstOrDefault(),
            Body: body
        );

        // Log the request synchronously (requestService is scoped, can't use after request ends)
        try
        {
            await requestService.CreateRequestAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log request to analytics");
        }

        await _next(context);
    }
}
