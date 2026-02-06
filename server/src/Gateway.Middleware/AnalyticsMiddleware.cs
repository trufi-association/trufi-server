using Gateway.Shared.Models;
using Gateway.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;

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

    public async Task InvokeAsync(HttpContext context, RequestLogQueue requestLogQueue, IProxyConfigProvider proxyConfigProvider)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip analytics for excluded paths
        if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Check if analytics is enabled for this route based on host
        var host = context.Request.Host.Value ?? "unknown";
        if (!IsAnalyticsEnabled(proxyConfigProvider, host))
        {
            await _next(context);
            return;
        }

        // Capture request data before processing
        var method = context.Request.Method;
        var uri = context.Request.Path + context.Request.QueryString;
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var deviceId = context.Request.Headers["X-Device-Id"].FirstOrDefault()
                    ?? context.Request.Headers["Device-Id"].FirstOrDefault();
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();

        // Capturar todos los headers y content-type del request
        var requestHeaders = SerializeHeaders(context.Request.Headers);
        var requestContentType = context.Request.ContentType;

        // Read request body if present
        string? requestBody = null;
        if (context.Request.ContentLength > 0)
        {
            if (PayloadHelper.ExceedsMaxSize(context.Request.ContentLength))
            {
                // Payload muy grande - guardar solo metadata
                requestBody = PayloadHelper.CreateLargePayloadPlaceholder(
                    requestContentType,
                    context.Request.ContentLength.Value);
            }
            else
            {
                // Payload normal - guardar completo
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }
        }

        // Replace response body stream to capture the response
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);

            // Capturar headers y content-type del response
            var responseHeaders = SerializeHeaders(context.Response.Headers);
            var responseContentType = context.Response.ContentType;

            // Read the response body
            responseBodyStream.Seek(0, SeekOrigin.Begin);

            string? responseBody = null;
            if (responseBodyStream.Length > 0)
            {
                if (PayloadHelper.ExceedsMaxSize(responseBodyStream.Length))
                {
                    // Response muy grande - guardar solo metadata
                    responseBody = PayloadHelper.CreateLargePayloadPlaceholder(
                        responseContentType,
                        responseBodyStream.Length);
                }
                else
                {
                    // Response normal - guardar completo
                    responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
                }
            }

            responseBodyStream.Seek(0, SeekOrigin.Begin);

            // Copy response back to original stream
            await responseBodyStream.CopyToAsync(originalBodyStream);

            // Log the request with response data
            var dto = new CreateRequestDto(
                Method: method,
                Uri: uri,
                Host: host,
                Ip: ip,
                DeviceId: deviceId,
                UserAgent: userAgent,
                RequestContentType: requestContentType,
                RequestHeaders: requestHeaders,
                Body: requestBody,
                StatusCode: context.Response.StatusCode,
                ResponseContentType: responseContentType,
                ResponseHeaders: responseHeaders,
                ResponseBody: responseBody
            );

            // Enqueue for async processing (< 1ms, non-blocking)
            try
            {
                await requestLogQueue.EnqueueAsync(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue request to analytics queue");
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static bool IsAnalyticsEnabled(IProxyConfigProvider proxyConfigProvider, string host)
    {
        var config = proxyConfigProvider.GetConfig();

        // Find route that matches this host
        var route = config.Routes.FirstOrDefault(r =>
            r.Match.Hosts?.Any(h => h.Equals(host, StringComparison.OrdinalIgnoreCase)) == true);

        if (route?.Metadata == null)
            return false;

        // Check Analytics metadata (default: false)
        return route.Metadata.TryGetValue("Analytics", out var value)
            && value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string SerializeHeaders(IHeaderDictionary headers)
    {
        var headerDict = new Dictionary<string, string[]>();
        foreach (var header in headers)
        {
            headerDict[header.Key] = header.Value.Where(v => v != null).ToArray()!;
        }
        return JsonSerializer.Serialize(headerDict);
    }
}
