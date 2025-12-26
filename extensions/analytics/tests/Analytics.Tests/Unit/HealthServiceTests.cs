using Analytics.Tests.TestFixtures;
using FluentAssertions;
using Shared.Services;

namespace Analytics.Tests.Unit;

public class HealthServiceTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly HealthService _sut;

    public HealthServiceTests()
    {
        _fixture = new InMemoryDbFixture();
        _sut = new HealthService(_fixture.Context);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task CheckDatabaseConnectionAsync_ShouldReturnTrue_WhenDatabaseIsAvailable()
    {
        // Act
        var result = await _sut.CheckDatabaseConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }
}
