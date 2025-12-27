namespace Api.Configuration;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddOpenApiConfiguration(this IServiceCollection services)
    {
        services.AddOpenApi();

        return services;
    }

    public static WebApplication UseOpenApiConfiguration(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/analytics-api/openapi/v1.json", "Analytics API");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
