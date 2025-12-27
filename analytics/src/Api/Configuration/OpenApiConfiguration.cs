using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Configuration;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddOpenApiConfiguration(this IServiceCollection services)
    {
        var basePath = Environment.GetEnvironmentVariable("BASE_PATH") ?? "";

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Servers =
                [
                    new OpenApiServer { Url = basePath }
                ];
                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication UseOpenApiConfiguration(this WebApplication app)
    {
        var basePath = Environment.GetEnvironmentVariable("BASE_PATH") ?? "";

        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"{basePath}/openapi/v1.json", "Analytics API");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
