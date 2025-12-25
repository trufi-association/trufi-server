using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=postgres;Database=analytics;Username=analytics;Password=analytics";

// Wait for database
await WaitForDatabase(connectionString);

// Initialize database
await InitializeDatabase(connectionString);

app.MapPost("/log", async (HttpContext context) =>
{
    var headers = context.Request.Headers;
    var body = "";

    using (var reader = new StreamReader(context.Request.Body))
    {
        body = await reader.ReadToEndAsync();
    }

    var deviceId = headers["X-Device-Id"].FirstOrDefault()
        ?? headers["Device-Id"].FirstOrDefault();

    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand(@"
        INSERT INTO requests (method, uri, host, ip, device_id, user_agent, body, received_at)
        VALUES (@method, @uri, @host, @ip, @device_id, @user_agent, @body, @received_at)", conn);

    cmd.Parameters.AddWithValue("method", headers["X-Original-Method"].FirstOrDefault() ?? "");
    cmd.Parameters.AddWithValue("uri", headers["X-Original-URI"].FirstOrDefault() ?? "");
    cmd.Parameters.AddWithValue("host", headers["X-Original-Host"].FirstOrDefault() ?? "");
    cmd.Parameters.AddWithValue("ip", headers["X-Real-IP"].FirstOrDefault() ?? "");
    cmd.Parameters.AddWithValue("device_id", (object?)deviceId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("user_agent", headers["User-Agent"].FirstOrDefault() ?? "");
    cmd.Parameters.AddWithValue("body", body);
    cmd.Parameters.AddWithValue("received_at", DateTime.UtcNow);

    await cmd.ExecuteNonQueryAsync();

    return Results.Ok();
});

app.MapGet("/health", async () =>
{
    try
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

app.Run("http://0.0.0.0:3000");

async Task WaitForDatabase(string connString)
{
    for (int i = 0; i < 30; i++)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            Console.WriteLine("Database connected");
            return;
        }
        catch
        {
            Console.WriteLine($"Waiting for database... ({i + 1}/30)");
            await Task.Delay(1000);
        }
    }
    throw new Exception("Failed to connect to database");
}

async Task InitializeDatabase(string connString)
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand(@"
        CREATE TABLE IF NOT EXISTS requests (
            id BIGSERIAL PRIMARY KEY,
            method VARCHAR(10) NOT NULL,
            uri TEXT NOT NULL,
            host VARCHAR(255) NOT NULL,
            ip VARCHAR(45),
            device_id VARCHAR(255),
            user_agent TEXT,
            body TEXT,
            received_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS idx_requests_host ON requests(host);
        CREATE INDEX IF NOT EXISTS idx_requests_received_at ON requests(received_at);
        CREATE INDEX IF NOT EXISTS idx_requests_device_id ON requests(device_id);
    ", conn);

    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("Database initialized");
}
