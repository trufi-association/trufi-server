using Gateway.Shared.Data;
using Gateway.Shared.Models;
using Gateway.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gateway.Shared.Tests;

public class RequestServiceTests
{
    /// <summary>A request with only the three required fields; every optional column stays null.</summary>
    private static CreateRequestDto Dto(string method, string uri, string host) =>
        new(method, uri, host, Ip: null, DeviceId: null, UserAgent: null,
            RequestContentType: null, RequestHeaders: null, Body: null);

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
            Method: "POST",
            Uri: "/api/test",
            Host: "example.com",
            Ip: "127.0.0.1",
            DeviceId: "device-123",
            UserAgent: "TestAgent",
            RequestContentType: "application/json",
            RequestHeaders: "{\"Accept\":\"application/json\"}",
            Body: "{\"q\":1}",
            StatusCode: 201,
            ResponseContentType: "text/plain",
            ResponseHeaders: "{\"Server\":\"test\"}",
            ResponseBody: "ok"
        );

        // Act
        var result = await service.CreateRequestAsync(dto);

        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal("POST", result.Method);
        Assert.Equal("/api/test", result.Uri);
        Assert.Equal("example.com", result.Host);
        Assert.Equal("device-123", result.DeviceId);
        Assert.Equal("127.0.0.1", result.Ip);
        Assert.Equal("TestAgent", result.UserAgent);
        Assert.Equal("application/json", result.RequestContentType);
        Assert.Equal("{\"Accept\":\"application/json\"}", result.RequestHeaders);
        Assert.Equal("{\"q\":1}", result.Body);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("text/plain", result.ResponseContentType);
        Assert.Equal("{\"Server\":\"test\"}", result.ResponseHeaders);
        Assert.Equal("ok", result.ResponseBody);
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldReturnFilteredRequests()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new RequestService(context);

        // Create test data
        await service.CreateRequestAsync(Dto("GET", "/api/v1", "host1.com"));
        await service.CreateRequestAsync(Dto("POST", "/api/v2", "host1.com"));
        await service.CreateRequestAsync(Dto("GET", "/api/v3", "host2.com"));

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
            await service.CreateRequestAsync(Dto("GET", $"/api/{i}", "example.com"));
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
        var created = await service.CreateRequestAsync(Dto("GET", "/test", "example.com"));

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
