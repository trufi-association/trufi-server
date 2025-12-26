using Analytics.Tests.TestFixtures;
using Api.Services;
using FluentAssertions;

namespace Analytics.Tests.Unit;

public class StatsServiceTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly StatsService _sut;

    public StatsServiceTests()
    {
        _fixture = new InMemoryDbFixture();
        _sut = new StatsService(_fixture.Context);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task GetGeneralStatsAsync_ShouldReturnCorrectCounts()
    {
        // Arrange
        var requests = TestDataBuilder.CreateRequestsForMultipleHosts(3, "host1.com", "host2.com");
        _fixture.Context.Requests.AddRange(requests);
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetGeneralStatsAsync(filter);

        // Assert
        result.TotalRequests.Should().Be(6);
        result.UniqueHosts.Should().Be(2);
        // Note: CreateRequestsForMultipleHosts creates device-1, device-2, device-3 for each host
        // but since deviceIds are the same across hosts, there are only 3 unique devices
        result.UniqueDevices.Should().Be(3);
    }

    [Fact]
    public async Task GetGeneralStatsAsync_ShouldFilterByHost()
    {
        // Arrange
        var requests = TestDataBuilder.CreateRequestsForMultipleHosts(3, "host1.com", "host2.com");
        _fixture.Context.Requests.AddRange(requests);
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter(Host: "host1.com");

        // Act
        var result = await _sut.GetGeneralStatsAsync(filter);

        // Assert
        result.TotalRequests.Should().Be(3);
        result.UniqueHosts.Should().Be(1);
    }

    [Fact]
    public async Task GetGeneralStatsAsync_ShouldFilterByDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddDays(-5)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddDays(-3)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddDays(-1)));
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter(From: now.AddDays(-4), To: now.AddDays(-2));

        // Act
        var result = await _sut.GetGeneralStatsAsync(filter);

        // Assert
        result.TotalRequests.Should().Be(1);
    }

    [Fact]
    public async Task GetGeneralStatsAsync_ShouldCountUniqueDevices_ExcludingNulls()
    {
        // Arrange
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "device-1"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "device-1"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "device-2"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: null));
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetGeneralStatsAsync(filter);

        // Assert
        result.TotalRequests.Should().Be(4);
        result.UniqueDevices.Should().Be(2);
    }

    [Fact]
    public async Task GetHourlyStatsAsync_ShouldGroupByHour()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var hour1 = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var hour2 = hour1.AddHours(-1);

        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: hour1.AddMinutes(10)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: hour1.AddMinutes(20)));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: hour2.AddMinutes(30)));
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetHourlyStatsAsync(filter);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(h => h.Count == 2);
        result.Should().Contain(h => h.Count == 1);
    }

    [Fact]
    public async Task GetHourlyStatsAsync_ShouldRespectMaxHoursLimit()
    {
        // Arrange
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(receivedAt: now.AddHours(-i)));
        }
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetHourlyStatsAsync(filter, maxHours: 5);

        // Assert
        result.Should().HaveCountLessOrEqualTo(5);
    }

    [Fact]
    public async Task GetTopEndpointsAsync_ShouldReturnTopEndpoints()
    {
        // Arrange
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(uri: "/api/popular"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(uri: "/api/popular"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(uri: "/api/popular"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(uri: "/api/less-popular"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(uri: "/api/less-popular"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(uri: "/api/rare"));
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetTopEndpointsAsync(filter);

        // Assert
        result.Should().HaveCount(3);
        result.First().Uri.Should().Be("/api/popular");
        result.First().Count.Should().Be(3);
    }

    [Fact]
    public async Task GetTopEndpointsAsync_ShouldRespectLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(uri: $"/api/endpoint{i}"));
        }
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetTopEndpointsAsync(filter, limit: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetDeviceStatsAsync_ShouldReturnDeviceStats()
    {
        // Arrange
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "active-device"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "active-device"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "active-device"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "less-active"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: null));
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetDeviceStatsAsync(filter);

        // Assert
        result.Should().HaveCount(2);
        result.First().DeviceId.Should().Be("active-device");
        result.First().Count.Should().Be(3);
    }

    [Fact]
    public async Task GetDeviceStatsAsync_ShouldExcludeNullDeviceIds()
    {
        // Arrange
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: "device-1"));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: null));
        _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: null));
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetDeviceStatsAsync(filter);

        // Assert
        result.Should().HaveCount(1);
        result.Should().OnlyContain(d => d.DeviceId != null);
    }

    [Fact]
    public async Task GetDeviceStatsAsync_ShouldRespectLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _fixture.Context.Requests.Add(TestDataBuilder.CreateRequest(deviceId: $"device-{i}"));
        }
        await _fixture.Context.SaveChangesAsync();

        var filter = new StatsFilter();

        // Act
        var result = await _sut.GetDeviceStatsAsync(filter, limit: 3);

        // Assert
        result.Should().HaveCount(3);
    }
}
