namespace Api.Configuration;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();

        return services;
    }

    public static WebApplication UseSwaggerConfiguration(this WebApplication app)
    {
        app.MapOpenApi();

        return app;
    }
}
