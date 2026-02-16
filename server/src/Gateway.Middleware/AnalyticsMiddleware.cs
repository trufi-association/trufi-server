using Gateway.Shared.Models;
using Gateway.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
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

        // Capture all headers and content-type from request
        var requestHeaders = SerializeHeaders(context.Request.Headers);
        var requestContentType = context.Request.ContentType;

        // Read request body if present
        string? requestBody = null;
        if (context.Request.ContentLength > 0)
        {
            if (PayloadHelper.IsBinaryContent(requestContentType))
            {
                // Binary content - don't save (prevents UTF-8 error in PostgreSQL)
                requestBody = null;
            }
            else if (PayloadHelper.ExceedsMaxSize(context.Request.ContentLength))
            {
                // Large payload - save only metadata
                requestBody = PayloadHelper.CreateLargePayloadPlaceholder(
                    requestContentType,
                    context.Request.ContentLength.Value);
            }
            else if (PayloadHelper.IsJsonContent(requestContentType))
            {
                // Text/JSON payload - save complete (sanitized)
                context.Request.EnableBuffering();
                // Decompress if Content-Encoding is present (gzip, deflate, br)
                var decompressedStream = GetDecompressedStream(
                    context.Request.Body,
                    context.Request.Headers.ContentEncoding.FirstOrDefault());
                using var reader = new StreamReader(decompressedStream, leaveOpen: true);
                var rawBody = await reader.ReadToEndAsync();
                requestBody = PayloadHelper.SanitizeString(rawBody);
                context.Request.Body.Position = 0;
            }
            // Unknown content-type - don't save for safety (null)
        }

        // Replace response body stream to capture the response
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);

            // Capture headers and content-type from response
            var responseHeaders = SerializeHeaders(context.Response.Headers);
            var responseContentType = context.Response.ContentType;

            // Read the response body
            responseBodyStream.Seek(0, SeekOrigin.Begin);

            string? responseBody = null;
            if (responseBodyStream.Length > 0)
            {
                if (PayloadHelper.IsBinaryContent(responseContentType))
                {
                    // Binary content - don't save (prevents UTF-8 error in PostgreSQL)
                    responseBody = null;
                }
                else if (PayloadHelper.ExceedsMaxSize(responseBodyStream.Length))
                {
                    // Large response - save only metadata
                    responseBody = PayloadHelper.CreateLargePayloadPlaceholder(
                        responseContentType,
                        responseBodyStream.Length);
                }
                else if (PayloadHelper.IsJsonContent(responseContentType))
                {
                    // Decompress if Content-Encoding is present (gzip, deflate, br)
                    var decompressedStream = GetDecompressedStream(
                        responseBodyStream,
                        context.Response.Headers.ContentEncoding.FirstOrDefault());
                    using var reader = new StreamReader(decompressedStream, leaveOpen: true);
                    var rawBody = await reader.ReadToEndAsync();
                    responseBody = PayloadHelper.SanitizeString(rawBody);
                }
                // Unknown content-type - don't save for safety (null)
            }

            responseBodyStream.Seek(0, SeekOrigin.Begin);

            // Copy response back to original stream
            await responseBodyStream.CopyToAsync(originalBodyStream);

            // Log the request with response data (sanitize ALL strings)
            var dto = new CreateRequestDto(
                Method: PayloadHelper.SanitizeString(method) ?? method,
                Uri: PayloadHelper.SanitizeString(uri) ?? uri,
                Host: PayloadHelper.SanitizeString(host) ?? host,
                Ip: PayloadHelper.SanitizeString(ip),
                DeviceId: PayloadHelper.SanitizeString(deviceId),
                UserAgent: PayloadHelper.SanitizeString(userAgent),
                RequestContentType: PayloadHelper.SanitizeString(requestContentType),
                RequestHeaders: requestHeaders, // Already sanitized in SerializeHeaders
                Body: requestBody, // Already sanitized when read
                StatusCode: context.Response.StatusCode,
                ResponseContentType: PayloadHelper.SanitizeString(responseContentType),
                ResponseHeaders: responseHeaders, // Already sanitized in SerializeHeaders
                ResponseBody: responseBody // Already sanitized when read
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

    /// <summary>
    /// Returns a decompressed stream if Content-Encoding is gzip, deflate, or br.
    /// Otherwise returns the original stream unchanged.
    /// </summary>
    private static Stream GetDecompressedStream(Stream stream, string? contentEncoding)
    {
        if (string.IsNullOrEmpty(contentEncoding))
            return stream;

        return contentEncoding.ToLowerInvariant() switch
        {
            "gzip" => new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true),
            "deflate" => new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true),
            "br" => new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true),
            _ => stream
        };
    }

    private static string SerializeHeaders(IHeaderDictionary headers)
    {
        var headerDict = new Dictionary<string, string[]>();
        foreach (var header in headers)
        {
            // Sanitize header values to remove null bytes
            var sanitizedValues = header.Value
                .Where(v => v != null)
                .Select(v => PayloadHelper.SanitizeString(v) ?? string.Empty)
                .ToArray();

            headerDict[header.Key] = sanitizedValues;
        }
        return JsonSerializer.Serialize(headerDict);
    }
}
