using System.IO.Compression;
using System.Text;
using Gateway.Shared.Models;
using Gateway.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Middleware.Tests;

/// <summary>
/// Issue #5: the size gate measured the compressed body and then decompressed,
/// so a 359 KB gzip of a 1.4 MB JSON was stored in full on every cold load.
/// The limit now applies to the decompressed text, JSON is the only body
/// copied, and the client always gets the wire bytes untouched.
/// </summary>
public class AnalyticsMiddlewareTests
{
    private const string Host = "planner.test";

    private static byte[] Gzip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(bytes);
        return output.ToArray();
    }

    /// <summary>JSON like the planner's search index: repetitive, so it gzips well.</summary>
    private static byte[] Json(int entries) =>
        Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Range(0, entries)
            .Select(i => $"{{\"name\":\"Parada {i}\",\"lat\":{-17.39 + i / 1e5:F5},\"lon\":-66.15}}")) + "]");

    private static IProxyConfigProvider Routes(bool analytics) => new InMemoryConfigProvider(
        [
            new RouteConfig
            {
                RouteId = "planner",
                ClusterId = "planner",
                Match = new RouteMatch { Hosts = [Host] },
                Metadata = new Dictionary<string, string> { ["Analytics"] = analytics ? "true" : "false" },
            },
        ],
        [new ClusterConfig { ClusterId = "planner" }]);

    private static RequestDelegate Respond(string contentType, byte[] body, string? contentEncoding = null) =>
        async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = contentType;
            if (contentEncoding != null)
                context.Response.Headers.ContentEncoding = contentEncoding;
            // Several writes, like a proxied body arrives.
            foreach (var chunk in body.Chunk(64 * 1024))
                await context.Response.Body.WriteAsync(chunk);
        };

    private static async Task<(CreateRequestDto? logged, byte[] wire)> Invoke(
        RequestDelegate next,
        Action<HttpContext>? configure = null,
        bool analytics = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(Host);
        context.Request.Path = "/api/plan";
        context.Request.Method = "GET";
        var wire = new MemoryStream();
        context.Response.Body = wire;
        configure?.Invoke(context);

        var queue = new RequestLogQueue();
        var middleware = new AnalyticsMiddleware(next, NullLogger<AnalyticsMiddleware>.Instance);
        await middleware.InvokeAsync(context, queue, Routes(analytics));

        Assert.Same(wire, context.Response.Body); // restored for the rest of the pipeline
        if (queue.Count == 0)
            return (null, wire.ToArray());
        await using var items = queue.DequeueAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await items.MoveNextAsync());
        return (items.Current, wire.ToArray());
    }

    [Fact]
    public async Task Gzipped_json_under_the_wire_limit_but_over_it_decompressed_is_a_placeholder()
    {
        var json = Json(30_000);
        var gz = Gzip(json);
        Assert.True(json.Length > PayloadHelper.MaxPayloadSize, $"fixture: {json.Length} B decompressed");
        Assert.True(gz.Length < PayloadHelper.MaxPayloadSize, $"fixture: {gz.Length} B on the wire");

        var (logged, wire) = await Invoke(Respond("application/json", gz, "gzip"));

        Assert.Equal(gz, wire);
        Assert.NotNull(logged);
        Assert.StartsWith("[LARGE PAYLOAD: application/json, ", logged!.ResponseBody);
        Assert.Contains("gzip on the wire, over 1 MB decompressed]", logged.ResponseBody);
        Assert.Equal(200, logged.StatusCode);
    }

    [Fact]
    public async Task Gzipped_small_json_is_logged_decompressed()
    {
        var json = Json(50);
        var gz = Gzip(json);

        var (logged, wire) = await Invoke(Respond("application/json; charset=utf-8", gz, "gzip"));

        Assert.Equal(gz, wire);
        Assert.Equal(Encoding.UTF8.GetString(json), logged!.ResponseBody);
        Assert.Equal("application/json; charset=utf-8", logged.ResponseContentType);
    }

    [Fact]
    public async Task Identity_json_is_logged_as_before_and_over_the_limit_is_a_placeholder()
    {
        var small = Json(50);
        var (loggedSmall, wireSmall) = await Invoke(Respond("application/json", small));
        Assert.Equal(small, wireSmall);
        Assert.Equal(Encoding.UTF8.GetString(small), loggedSmall!.ResponseBody);

        var big = Json(30_000);
        var (loggedBig, wireBig) = await Invoke(Respond("application/json", big));
        Assert.Equal(big, wireBig);
        Assert.Equal(PayloadHelper.CreateLargePayloadPlaceholder("application/json", big.Length), loggedBig!.ResponseBody);
    }

    [Fact]
    public async Task A_static_file_is_forwarded_untouched_and_not_logged_as_a_body()
    {
        var js = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("function f(a,b){return a+b;}\n", 200_000)));
        Assert.True(js.Length > 5_000_000);

        var (logged, wire) = await Invoke(
            Respond("text/javascript", Gzip(js), "gzip"),
            context => context.Request.Path = "/main.dart.js");

        Assert.Equal(Gzip(js), wire);
        Assert.NotNull(logged);
        Assert.Null(logged!.ResponseBody);
        Assert.Equal("/main.dart.js", logged.Uri);
    }

    [Fact]
    public async Task Binary_content_is_still_never_stored()
    {
        var png = Enumerable.Range(0, 4096).Select(i => (byte)((i * 7919 + 13) & 0xff)).ToArray();

        var (logged, wire) = await Invoke(Respond("image/png", png));

        Assert.Equal(png, wire);
        Assert.Null(logged!.ResponseBody);
    }

    // deflate is not in the list on purpose: a raw DEFLATE decoder accepts
    // almost any byte sequence as a stream, so garbage comes out as garbage
    // text rather than as an exception.
    [Theory]
    [InlineData("gzip")]
    [InlineData("br")] // BrotliStream throws InvalidOperationException, not InvalidDataException
    public async Task A_body_that_does_not_decompress_is_logged_as_null_not_as_an_error(string encoding)
    {
        var garbage = Encoding.UTF8.GetBytes("{\"not\":\"compressed\"}");

        var (logged, wire) = await Invoke(Respond("application/json", garbage, encoding));

        Assert.Equal(garbage, wire);
        Assert.Null(logged!.ResponseBody);
    }

    [Fact]
    public async Task An_encoding_the_gateway_cannot_inflate_is_not_logged_as_garbage_text()
    {
        var zstdLike = Gzip(Json(50)); // bytes we cannot read when they are labelled zstd

        var (logged, wire) = await Invoke(Respond("application/json", zstdLike, "zstd"));

        Assert.Equal(zstdLike, wire);
        Assert.Null(logged!.ResponseBody);
    }

    [Fact]
    public async Task A_large_text_response_that_is_not_json_keeps_its_size_placeholder()
    {
        var script = Encoding.UTF8.GetBytes(new string('x', 5 * 1024 * 1024));

        var (logged, wire) = await Invoke(Respond("text/javascript", script));

        Assert.Equal(script, wire);
        Assert.StartsWith("[LARGE PAYLOAD: text/javascript", logged!.ResponseBody);
    }

    [Fact]
    public async Task Gzipped_request_json_over_the_limit_decompressed_is_a_placeholder_and_the_body_is_rewound()
    {
        var json = Json(30_000);
        var gz = Gzip(json);
        long seenByNext = -1;

        var (logged, _) = await Invoke(
            async context =>
            {
                using var reader = new MemoryStream();
                await context.Request.Body.CopyToAsync(reader);
                seenByNext = reader.Length;
                context.Response.ContentType = "application/json";
                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("{\"ok\":true}"));
            },
            context =>
            {
                context.Request.Method = "POST";
                context.Request.ContentType = "application/json";
                context.Request.Headers.ContentEncoding = "gzip";
                context.Request.ContentLength = gz.Length;
                context.Request.Body = new MemoryStream(gz);
            });

        Assert.Equal(gz.Length, seenByNext);
        Assert.StartsWith("[LARGE PAYLOAD: application/json, ", logged!.Body);
        Assert.Contains("gzip on the wire, over 1 MB decompressed]", logged.Body);
        Assert.Equal("{\"ok\":true}", logged.ResponseBody);
    }

    [Fact]
    public async Task A_route_without_analytics_is_passed_through_without_a_tee()
    {
        var json = Json(50);
        Stream? bodySeenByNext = null;

        var (logged, wire) = await Invoke(
            async context =>
            {
                bodySeenByNext = context.Response.Body;
                context.Response.ContentType = "application/json";
                await context.Response.Body.WriteAsync(json);
            },
            analytics: false);

        Assert.Null(logged);
        Assert.Equal(json, wire);
        Assert.IsType<MemoryStream>(bodySeenByNext);
    }
}
