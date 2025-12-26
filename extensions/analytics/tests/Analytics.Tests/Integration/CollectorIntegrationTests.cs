using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Analytics.Tests.Integration;

public class CollectorIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Collector.Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Collector.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services =>
                {
                    // Remove ALL EF Core and DbContext related services
                    var servicesToRemove = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<AnalyticsDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(AnalyticsDbContext) ||
                        (d.ServiceType.IsGenericType &&
                         d.ServiceType.GetGenericTypeDefinition().FullName?.Contains("DbContextOptions") == true) ||
                        d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                        d.ImplementationType?.FullName?.StartsWith("Npgsql") == true).ToList();

                    foreach (var descriptor in servicesToRemove)
                        services.Remove(descriptor);

                    // Add in-memory database
                    services.AddDbContext<AnalyticsDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                });
            });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Health_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Log_ShouldCreateRequest()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/log")
        {
            Content = new StringContent("{\"data\": \"test\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Original-Method", "GET");
        request.Headers.Add("X-Original-URI", "/api/test");
        request.Headers.Add("X-Original-Host", "example.com");
        request.Headers.Add("X-Real-IP", "192.168.1.1");
        request.Headers.Add("X-Device-Id", "device-123");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify data was saved
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var savedRequest = await context.Requests.FirstOrDefaultAsync();
        savedRequest.Should().NotBeNull();
        savedRequest!.Method.Should().Be("GET");
        savedRequest.Uri.Should().Be("/api/test");
        savedRequest.Host.Should().Be("example.com");
        savedRequest.Ip.Should().Be("192.168.1.1");
        savedRequest.DeviceId.Should().Be("device-123");
        savedRequest.Body.Should().Be("{\"data\": \"test\"}");
    }

    [Fact]
    public async Task Log_ShouldHandleMissingOptionalHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/log")
        {
            Content = new StringContent("", Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("X-Original-Method", "POST");
        request.Headers.Add("X-Original-URI", "/api/data");
        request.Headers.Add("X-Original-Host", "test.com");
        // Not adding X-Real-IP, X-Device-Id

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var savedRequest = await context.Requests.FirstOrDefaultAsync();
        savedRequest.Should().NotBeNull();
        savedRequest!.Ip.Should().BeNull();
        savedRequest.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task Log_ShouldUseFallbackDeviceIdHeader()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/log")
        {
            Content = new StringContent("", Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("X-Original-Method", "GET");
        request.Headers.Add("X-Original-URI", "/api/test");
        request.Headers.Add("X-Original-Host", "example.com");
        request.Headers.Add("Device-Id", "fallback-device"); // Fallback header

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var savedRequest = await context.Requests.FirstOrDefaultAsync();
        savedRequest.Should().NotBeNull();
        savedRequest!.DeviceId.Should().Be("fallback-device");
    }

    [Fact]
    public async Task Log_ShouldPreferXDeviceIdOverDeviceId()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/log")
        {
            Content = new StringContent("", Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("X-Original-Method", "GET");
        request.Headers.Add("X-Original-URI", "/api/test");
        request.Headers.Add("X-Original-Host", "example.com");
        request.Headers.Add("X-Device-Id", "primary-device");
        request.Headers.Add("Device-Id", "fallback-device");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var savedRequest = await context.Requests.FirstOrDefaultAsync();
        savedRequest.Should().NotBeNull();
        savedRequest!.DeviceId.Should().Be("primary-device");
    }
}
