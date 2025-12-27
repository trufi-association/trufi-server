using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Analytics.Tests.TestFixtures;

public class InMemoryDbFixture : IDisposable
{
    public AnalyticsDbContext Context { get; }

    public InMemoryDbFixture()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        Context = new AnalyticsDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
    }
}
