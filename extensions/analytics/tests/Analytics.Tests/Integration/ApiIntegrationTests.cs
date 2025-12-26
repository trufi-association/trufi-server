using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Shared.Models;

namespace Analytics.Tests.Integration;

public class ApiIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Api.Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Api.Program>()
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
    public async Task GetLogs_ShouldReturnEmptyList_WhenNoData()
    {
        // Act
        var response = await _client.GetAsync("/logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("[]");
    }

    [Fact]
    public async Task GetLogs_ShouldReturnLogs_WhenDataExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        context.Requests.Add(new Request
        {
            Method = "GET",
            Uri = "/api/test",
            Host = "example.com",
            ReceivedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<List<LogResponse>>();
        logs.Should().NotBeEmpty();
        logs!.First().Method.Should().Be("GET");
    }

    [Fact]
    public async Task GetLogs_ShouldFilterByHost()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        context.Requests.AddRange(
            new Request { Method = "GET", Uri = "/api/1", Host = "host1.com", ReceivedAt = DateTime.UtcNow },
            new Request { Method = "GET", Uri = "/api/2", Host = "host2.com", ReceivedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/logs?host=host1.com");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<List<LogResponse>>();
        logs.Should().HaveCount(1);
        logs!.First().Host.Should().Be("host1.com");
    }

    [Fact]
    public async Task GetStats_ShouldReturnStats()
    {
        // Act
        var response = await _client.GetAsync("/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<StatsResponse>();
        stats.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatsHourly_ShouldReturnHourlyStats()
    {
        // Act
        var response = await _client.GetAsync("/stats/hourly");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStatsEndpoints_ShouldReturnEndpointStats()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        context.Requests.AddRange(
            new Request { Method = "GET", Uri = "/api/popular", Host = "example.com", ReceivedAt = DateTime.UtcNow },
            new Request { Method = "GET", Uri = "/api/popular", Host = "example.com", ReceivedAt = DateTime.UtcNow },
            new Request { Method = "GET", Uri = "/api/rare", Host = "example.com", ReceivedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/stats/endpoints");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var endpoints = await response.Content.ReadFromJsonAsync<List<EndpointResponse>>();
        endpoints.Should().NotBeEmpty();
        endpoints!.First().Uri.Should().Be("/api/popular");
    }

    [Fact]
    public async Task GetStatsDevices_ShouldReturnDeviceStats()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        context.Requests.AddRange(
            new Request { Method = "GET", Uri = "/api/1", Host = "example.com", DeviceId = "device-1", ReceivedAt = DateTime.UtcNow },
            new Request { Method = "GET", Uri = "/api/2", Host = "example.com", DeviceId = "device-1", ReceivedAt = DateTime.UtcNow },
            new Request { Method = "GET", Uri = "/api/3", Host = "example.com", DeviceId = "device-2", ReceivedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/stats/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var devices = await response.Content.ReadFromJsonAsync<List<DeviceResponse>>();
        devices.Should().NotBeEmpty();
    }

    // Response DTOs for deserialization
    private record LogResponse(
        long Id,
        string Method,
        string Uri,
        string Host,
        string? Ip,
        string? DeviceId,
        string? UserAgent,
        string? Body,
        DateTime ReceivedAt
    );

    private record StatsResponse(
        int total_requests,
        int unique_devices,
        int unique_hosts
    );

    private record EndpointResponse(string Uri, int Count);
    private record DeviceResponse(string device_id, int Count);
}
