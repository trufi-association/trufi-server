namespace Shared.Services;

public interface IHealthService
{
    Task<bool> CheckDatabaseConnectionAsync(CancellationToken cancellationToken = default);
}
