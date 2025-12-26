# Analytics Extension

Real-time request logging to PostgreSQL.

## Architecture

```
nginx (mirror) → Collector → PostgreSQL ← Api
```

- **Collector**: Receives mirrored requests from nginx and stores them (port 3000)
- **Api**: Exposes REST endpoints for querying logs and statistics (port 3001)
- **PostgreSQL**: Stores all request data

All requests are mirrored to the collector service without affecting response time.

## Project Structure

```
analytics/
├── Analytics.sln
├── docker-compose.yml
├── src/
│   ├── Shared/           # Models, DbContext, Services, Migrations
│   ├── Collector/        # Receives and stores requests
│   └── Api/              # REST API for querying
└── tests/
    └── Analytics.Tests/  # Unit and integration tests
```

## Setup

1. Start the analytics stack:
```bash
cd extensions/analytics
docker compose up -d
```

2. Migrations run automatically on startup (Collector applies them)

3. Configure nginx proxy for the API (see below)

## Expose API via Nginx Proxy

Add this to your domain's nginx config (in `data/nginx/yourdomain.conf`), inside the `server` block for port 443:

```nginx
    # Analytics API
    location /analytics-api/ {
        proxy_pass http://analytics-api:3001/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
```

Then reload nginx:
```bash
docker compose exec nginx_single_server nginx -s reload
```

## API Endpoints

### Get Logs
```
GET /logs?from=2024-01-01&to=2024-01-31&host=example.com&method=GET&deviceId=abc123&limit=100&offset=0
```

All parameters are optional.

### Get Statistics
```
GET /stats?from=2024-01-01&to=2024-01-31&host=example.com
```

Response:
```json
{
  "total_requests": 12345,
  "unique_devices": 456,
  "unique_hosts": 3
}
```

### Get Hourly Stats
```
GET /stats/hourly?from=2024-01-01&to=2024-01-31&host=example.com
```

### Get Top Endpoints
```
GET /stats/endpoints?from=2024-01-01&to=2024-01-31&host=example.com&limit=20
```

### Get Device Stats
```
GET /stats/devices?from=2024-01-01&to=2024-01-31&limit=20
```

## Database Schema

```sql
requests (
    id BIGSERIAL PRIMARY KEY,
    method VARCHAR(10),
    uri TEXT,
    host VARCHAR(255),
    ip VARCHAR(45),
    device_id VARCHAR(255),
    user_agent TEXT,
    body TEXT,
    received_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE
)
```

Indexes on: `host`, `received_at`, `device_id`

## Migrations

Migrations are managed with Entity Framework Core and run automatically when Collector starts.

### Create a new migration

```bash
cd extensions/analytics

# Install EF tools if needed
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add MigrationName \
  --project src/Shared \
  --startup-project src/Collector
```

### Apply migrations manually

```bash
dotnet ef database update \
  --project src/Shared \
  --startup-project src/Collector
```

### Rollback migration

```bash
dotnet ef database update PreviousMigrationName \
  --project src/Shared \
  --startup-project src/Collector
```

## Development

### Build locally

```bash
cd extensions/analytics
dotnet build
```

### Run locally

```bash
# Start postgres
docker compose up -d postgres

# Run collector
dotnet run --project src/Collector

# Run api (in another terminal)
dotnet run --project src/Api
```

### Rebuild containers

```bash
docker compose build
docker compose up -d
```

## Testing

Tests use an in-memory database, no external dependencies required.

### Run all tests

```bash
cd extensions/analytics
dotnet test
```

### Run with coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run specific test class

```bash
dotnet test --filter "FullyQualifiedName~RequestServiceTests"
```

### Test structure

- **Unit tests** (`tests/Analytics.Tests/Unit/`): Test services in isolation with in-memory database
- **Integration tests** (`tests/Analytics.Tests/Integration/`): Test API endpoints with WebApplicationFactory
