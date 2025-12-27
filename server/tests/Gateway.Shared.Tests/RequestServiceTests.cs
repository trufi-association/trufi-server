using Gateway.Shared.Data;
using Gateway.Shared.Models;
using Gateway.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gateway.Shared.Tests;

public class RequestServiceTests
{
    private AnalyticsDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AnalyticsDbContext(options);
    }

    [Fact]
    public async Task CreateRequestAsync_ShouldCreateRequest()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new RequestService(context);
        var dto = new CreateRequestDto(
            Method: "GET",
            Uri: "/api/test",
            Host: "example.com",
            Ip: "127.0.0.1",
            DeviceId: "device-123",
            UserAgent: "TestAgent",
            Body: null
        );

        // Act
        var result = await service.CreateRequestAsync(dto);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("GET", result.Method);
        Assert.Equal("/api/test", result.Uri);
        Assert.Equal("example.com", result.Host);
        Assert.Equal("device-123", result.DeviceId);
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldReturnFilteredRequests()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new RequestService(context);

        // Create test data
        await service.CreateRequestAsync(new CreateRequestDto("GET", "/api/v1", "host1.com", null, null, null, null));
        await service.CreateRequestAsync(new CreateRequestDto("POST", "/api/v2", "host1.com", null, null, null, null));
        await service.CreateRequestAsync(new CreateRequestDto("GET", "/api/v3", "host2.com", null, null, null, null));

        // Act
        var filter = new RequestFilter(Host: "host1.com");
        var results = await service.GetRequestsAsync(filter);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("host1.com", r.Host));
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldApplyLimitAndOffset()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new RequestService(context);

        for (int i = 0; i < 10; i++)
        {
            await service.CreateRequestAsync(new CreateRequestDto("GET", $"/api/{i}", "example.com", null, null, null, null));
        }

        // Act
        var filter = new RequestFilter(Limit: 3, Offset: 2);
        var results = await service.GetRequestsAsync(filter);

        // Assert
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GetRequestByIdAsync_ShouldReturnRequest_WhenExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new RequestService(context);
        var created = await service.CreateRequestAsync(new CreateRequestDto("GET", "/test", "example.com", null, null, null, null));

        // Act
        var result = await service.GetRequestByIdAsync(created.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetRequestByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new RequestService(context);

        // Act
        var result = await service.GetRequestByIdAsync(999);

        // Assert
        Assert.Null(result);
    }
}
