# Analytics Extension

Real-time request logging to PostgreSQL with Metabase dashboard.

## Architecture

```
nginx (mirror) → Go Logger → PostgreSQL ← Metabase
```

All requests are mirrored to the analytics service without affecting response time.

## Setup

1. Start the analytics stack:
```bash
cd extensions/analytics
docker compose up -d
```

2. Access Metabase at `http://localhost:3001`
   - First time setup will ask you to create an admin account
   - Connect to database:
     - Type: PostgreSQL
     - Host: postgres
     - Port: 5432
     - Database: analytics
     - User: analytics
     - Password: analytics

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
    received_at TIMESTAMP,
    created_at TIMESTAMP
)
```

## Useful Queries

```sql
-- Requests per hour
SELECT date_trunc('hour', received_at) as hour, COUNT(*)
FROM requests
GROUP BY hour ORDER BY hour DESC;

-- Top endpoints
SELECT uri, COUNT(*) as hits
FROM requests
GROUP BY uri ORDER BY hits DESC LIMIT 20;

-- Requests by device
SELECT device_id, COUNT(*) as requests
FROM requests
WHERE device_id IS NOT NULL
GROUP BY device_id ORDER BY requests DESC;

-- Requests per domain
SELECT host, COUNT(*) as requests
FROM requests
GROUP BY host ORDER BY requests DESC;
```
