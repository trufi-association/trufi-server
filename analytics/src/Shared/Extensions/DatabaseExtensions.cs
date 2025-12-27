using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Shared.Extensions;

public static class DatabaseExtensions
{
    public static async Task WaitForDatabaseAsync(this IServiceProvider services, int maxRetries = 30)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await db.Database.CanConnectAsync();
                Console.WriteLine("Database connected");
                return;
            }
            catch
            {
                Console.WriteLine($"Waiting for database... ({i + 1}/{maxRetries})");
                await Task.Delay(1000);
            }
        }

        throw new Exception("Could not connect to database after maximum retries");
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, int maxRetries = 30)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await db.Database.MigrateAsync();
                Console.WriteLine("Database migrated successfully");
                return;
            }
            catch
            {
                Console.WriteLine($"Waiting for database... ({i + 1}/{maxRetries})");
                await Task.Delay(1000);
            }
        }

        throw new Exception("Could not migrate database after maximum retries");
    }
}
