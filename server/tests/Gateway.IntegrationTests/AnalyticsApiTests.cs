using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Gateway.IntegrationTests;

public class AnalyticsApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AnalyticsApiTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLogs_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/analytics-api/logs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_WithFilters_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/analytics-api/logs?limit=10&host=example.com");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/analytics-api/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("total_requests", content);
        Assert.Contains("unique_devices", content);
        Assert.Contains("unique_hosts", content);
    }

    [Fact]
    public async Task GetStatsHourly_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/analytics-api/stats/hourly");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatsEndpoints_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/analytics-api/stats/endpoints");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatsDevices_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/analytics-api/stats/devices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
