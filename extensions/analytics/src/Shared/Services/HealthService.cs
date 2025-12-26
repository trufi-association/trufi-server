using Shared.Data;

namespace Shared.Services;

public class HealthService : IHealthService
{
    private readonly AnalyticsDbContext _context;

    public HealthService(AnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CheckDatabaseConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Database.CanConnectAsync(cancellationToken);
    }
}
