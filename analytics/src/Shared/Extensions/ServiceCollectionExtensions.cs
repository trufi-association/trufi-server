using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Shared.Services;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AnalyticsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IHealthService, HealthService>();

        return services;
    }

    public static IServiceCollection AddAnalyticsServicesWithInMemoryDb(this IServiceCollection services, string databaseName = "TestDb")
    {
        services.AddDbContext<AnalyticsDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IHealthService, HealthService>();

        return services;
    }
}
