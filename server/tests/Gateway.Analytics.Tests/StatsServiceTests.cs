using Gateway.Analytics.Services;
using Gateway.Shared.Data;
using Gateway.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gateway.Analytics.Tests;

public class StatsServiceTests
{
    private AnalyticsDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AnalyticsDbContext(options);
    }

    private async Task SeedTestData(AnalyticsDbContext context)
    {
        var requests = new[]
        {
            new Request { Method = "GET", Uri = "/api/v1", Host = "host1.com", DeviceId = "device-1", ReceivedAt = DateTime.UtcNow.AddHours(-1) },
            new Request { Method = "GET", Uri = "/api/v1", Host = "host1.com", DeviceId = "device-1", ReceivedAt = DateTime.UtcNow.AddHours(-1) },
            new Request { Method = "POST", Uri = "/api/v2", Host = "host1.com", DeviceId = "device-2", ReceivedAt = DateTime.UtcNow.AddHours(-2) },
            new Request { Method = "GET", Uri = "/api/v3", Host = "host2.com", DeviceId = "device-3", ReceivedAt = DateTime.UtcNow.AddHours(-3) },
        };

        context.Requests.AddRange(requests);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetGeneralStatsAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var service = new StatsService(context);

        // Act
        var stats = await service.GetGeneralStatsAsync(new StatsFilter());

        // Assert
        Assert.Equal(4, stats.TotalRequests);
        Assert.Equal(3, stats.UniqueDevices);
        Assert.Equal(2, stats.UniqueHosts);
    }

    [Fact]
    public async Task GetGeneralStatsAsync_ShouldFilterByHost()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var service = new StatsService(context);

        // Act
        var stats = await service.GetGeneralStatsAsync(new StatsFilter(Host: "host1.com"));

        // Assert
        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(2, stats.UniqueDevices);
        Assert.Equal(1, stats.UniqueHosts);
    }

    [Fact]
    public async Task GetHourlyStatsAsync_ShouldGroupByHour()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var service = new StatsService(context);

        // Act
        var stats = await service.GetHourlyStatsAsync(new StatsFilter());

        // Assert
        Assert.True(stats.Count > 0);
        Assert.All(stats, s => Assert.True(s.Count > 0));
    }

    [Fact]
    public async Task GetTopEndpointsAsync_ShouldReturnOrderedByCount()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var service = new StatsService(context);

        // Act
        var endpoints = await service.GetTopEndpointsAsync(new StatsFilter(), limit: 10);

        // Assert
        Assert.True(endpoints.Count > 0);
        Assert.Equal("/api/v1", endpoints[0].Uri); // Most frequent
        Assert.Equal(2, endpoints[0].Count);
    }

    [Fact]
    public async Task GetDeviceStatsAsync_ShouldReturnOrderedByCount()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var service = new StatsService(context);

        // Act
        var devices = await service.GetDeviceStatsAsync(new StatsFilter(), limit: 10);

        // Assert
        Assert.True(devices.Count > 0);
        Assert.Equal("device-1", devices[0].DeviceId); // Most frequent
        Assert.Equal(2, devices[0].Count);
    }

    [Fact]
    public async Task GetGeneralStatsAsync_ShouldFilterByDateRange()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedTestData(context);
        var service = new StatsService(context);

        // Act - only get requests from last 2 hours
        var filter = new StatsFilter(From: DateTime.UtcNow.AddHours(-2));
        var stats = await service.GetGeneralStatsAsync(filter);

        // Assert
        Assert.True(stats.TotalRequests < 4);
    }
}
