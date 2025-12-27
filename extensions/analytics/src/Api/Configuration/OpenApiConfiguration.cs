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

        app.MapGet("/docs", () => Results.Content("""
            <!DOCTYPE html>
            <html>
            <head>
                <title>Analytics API</title>
                <meta charset="utf-8"/>
                <script type="module" src="https://unpkg.com/rapidoc/dist/rapidoc-min.js"></script>
            </head>
            <body>
                <rapi-doc
                    spec-url="/analytics-api/openapi/v1.json"
                    theme="dark"
                    bg-color="#1a1a2e"
                    text-color="#eaeaea"
                    primary-color="#00d4aa"
                    render-style="focused"
                    show-header="false"
                    allow-try="true"
                ></rapi-doc>
            </body>
            </html>
            """, "text/html"));

        return app;
    }
}
