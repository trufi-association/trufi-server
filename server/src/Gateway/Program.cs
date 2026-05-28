using Gateway.Analytics.Services;
using Gateway.Middleware;
using Gateway.Shared.Data;
using Gateway.Shared.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Database=analytics;Username=analytics;Password=analytics";
builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IStatsService, StatsService>();

// Async Request Logging (High Performance)
builder.Services.AddSingleton<RequestLogQueue>();
builder.Services.AddHostedService<RequestLogWriter>();

// Controllers (Analytics API)
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Gateway.Analytics.Controllers.LogsController).Assembly);

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// CORS: allow browser-side callers whose Origin host matches one of the
// domains this gateway is configured to serve (LettuceEncrypt.DomainNames).
// This keeps the policy generic across deployments — each deployer's own
// domains automatically become the CORS allowlist, with no extra config.
var allowedHosts = (builder.Configuration
        .GetSection("LettuceEncrypt:DomainNames")
        .Get<string[]>() ?? Array.Empty<string>())
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
              {
                  if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                  // Always allow loopback origins so developers can hit
                  // this gateway from `flutter run -d web-server` (or any
                  // local dev server) without having to add their machine
                  // to the configured DomainNames list.
                  if (uri.Host == "localhost" || uri.Host == "127.0.0.1") return true;
                  return allowedHosts.Contains(uri.Host);
              })
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Let's Encrypt (only in production)
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddLettuceEncrypt();
}

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Analytics API", Version = "v1" });
});

var app = builder.Build();

// Apply migrations (skip in test environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    db.Database.Migrate();
}

// Swagger UI
app.UseSwagger(c => c.RouteTemplate = "analytics-api/swagger/{documentName}/swagger.json");
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/analytics-api/swagger/v1/swagger.json", "Analytics API v1");
    c.RoutePrefix = "analytics-api/swagger";
});

// Analytics middleware - captures all requests (except analytics-api)
app.UseMiddleware<AnalyticsMiddleware>();

// Map controllers (Analytics API endpoints)
app.MapControllers();

app.UseCors();

// YARP reverse proxy
app.MapReverseProxy();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
