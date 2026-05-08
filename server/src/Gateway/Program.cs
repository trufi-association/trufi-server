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

// Permissive CORS so the planner web build (browser) can call OTP
// 1.5 / OTP 2.8 endpoints proxied through this gateway. Without
// this, browsers block cross-origin XHR with "No
// Access-Control-Allow-Origin header" because OTP itself doesn't
// emit CORS headers and YARP doesn't add them either. The Trufi
// Planner remote (`/api`) emits CORS itself, which is why that one
// already worked from the web — only the OTP-backed routes needed
// help.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
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
