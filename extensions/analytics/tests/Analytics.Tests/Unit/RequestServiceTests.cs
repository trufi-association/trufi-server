using Analytics.Tests.TestFixtures;
using FluentAssertions;
using Shared.Services;

namespace Analytics.Tests.Unit;

public class RequestServiceTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly RequestService _sut;

    public RequestServiceTests()
    {
        _fixture = new InMemoryDbFixture();
        _sut = new RequestService(_fixture.Context);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task CreateRequestAsync_ShouldCreateRequest()
    {
        // Arrange
        var dto = new CreateRequestDto(
            Method: "GET",
            Uri: "/api/test",
            Host: "example.com",
            Ip: "192.168.1.1",
            DeviceId: "device-123",
            UserAgent: "TestAgent/1.0",
            Body: null
        );

        // Act
        var result = await _sut.CreateRequestAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Method.Should().Be("GET");
        result.Uri.Should().Be("/api/test");
        result.Host.Should().Be("example.com");
        result.DeviceId.Should().Be("device-123");
    }

    [Fact]
    public async Task CreateRequestAsync_ShouldPersistToDatabase()
    {
        // Arrange
        var dto = new CreateRequestDto(
            Method: "POST",
            Uri: "/api/data",
            Host: "test.com",
            Ip: "10.0.0.1",
            DeviceId: "device-456",
            UserAgent: "Mozilla/5.0",
            Body: "{\"test\": true}"
        );

        // Act
        var created = await _sut.CreateRequestAsync(dto);

        // Assert
        var retrieved = await _sut.GetRequestByIdAsync(created.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Body.Should().Be("{\"test\": true}");
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldReturnAllRequests_WhenNoFilter()
    {
        // Arrange
        var requests = TestDataBuilder.CreateRequests(5);
        _fixture.Context.Requests.AddRange(requests);
        await _fixture.Context.SaveChangesAsync();

        var filter = new RequestFilter();

        // Act
        var result = await _sut.GetRequestsAsync(filter);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldFilterByHost()
    {
        // Arrange
        var requests = TestDataBuilder.CreateRequestsForMultipleHosts(3, "host1.com", "host2.com");
        _fixture.Context.Requests.AddRange(requests);
        await _fixture.Context.SaveChangesAsync();

        var filter = new RequestFilter(Host: "host1.com");

        // Act
        var result = await _sut.GetRequestsAsync(filter);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(r => r.Host == "host1.com");
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldFilterByMethod()
    {
        // Arrange
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(method: "GET"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(method: "POST"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(method: "GET"));
        await _fixture.Context.SaveChangesAsync();

        var filter = new RequestFilter(Method: "GET");

        // Act
        var result = await _sut.GetRequestsAsync(filter);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.Method == "GET");
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldFilterByDeviceId()
    {
        // Arrange
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "device-1"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "device-2"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "device-1"));
        await _fixture.Context.SaveChangesAsync();

        var filter = new RequestFilter(DeviceId: "device-1");

        // Act
        var result = await _sut.GetRequestsAsync(filter);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.DeviceId == "device-1");
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldFilterByDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddDays(-5)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddDays(-3)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddDays(-1)));
        await _fixture.Context.SaveChangesAsync();

        var filter = new RequestFilter(From: now.AddDays(-4), To: now.AddDays(-2));

        // Act
        var result = await _sut.GetRequestsAsync(filter);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldApplyLimitAndOffset()
    {
        // Arrange
        var requests = TestDataBuilder.CreateRequests(10);
        _fixture.Context.Requests.AddRange(requests);
        await _fixture.Context.SaveChangesAsync();

        var filter = new RequestFilter(Limit: 3, Offset: 2);

        // Act
        var result = await _sut.GetRequestsAsync(filter);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRequestsAsync_ShouldOrderByReceivedAtDescending()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddHours(-2)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddHours(-1)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now));
        await _fixture.Context.SaveChangesAsync();

        var filter = new RequestFilter();

        // Act
        var result = await _sut.GetRequestsAsync(filter);

        // Assert
        result.Should().BeInDescendingOrder(r => r.ReceivedAt);
    }

    [Fact]
    public async Task GetRequestByIdAsync_ShouldReturnRequest_WhenExists()
    {
        // Arrange
        var request = TestDataBuilder.CreateRequest();
        _fixture.Context.Requests.Add(request);
        await _fixture.Context.SaveChangesAsync();

        // Act
        var result = await _sut.GetRequestByIdAsync(request.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(request.Id);
    }

    [Fact]
    public async Task GetRequestByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _sut.GetRequestByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }
}
