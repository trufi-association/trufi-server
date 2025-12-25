# Analytics Extension

Real-time request logging to PostgreSQL with Grafana dashboard.

## Architecture

```
nginx (mirror) → Go Logger → PostgreSQL ← Grafana
```

All requests are mirrored to the analytics service without affecting response time.

## Setup

1. Start the analytics stack:
```bash
cd extensions/analytics
docker compose up -d
```

2. Configure nginx proxy (see below)

3. Access Grafana at `https://yourdomain.com/grafana/`
   - Default credentials: `admin` / `admin`

## Expose via Nginx Proxy

Add this to your domain's nginx config (in `data/nginx/yourdomain.conf`), inside the `server` block for port 443:

```nginx
    # Grafana dashboard
    location /grafana/ {
        proxy_pass http://grafana:3000/grafana/;
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

## Configure Grafana Data Source

1. Go to **Connections** → **Data sources** → **Add data source**
2. Select **PostgreSQL**
3. Configure:
   - Host: `postgres:5432`
   - Database: `analytics`
   - User: `analytics`
   - Password: `analytics`
   - TLS/SSL Mode: `disable`
4. Click **Save & test**

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

## Example Grafana Queries

### Requests per hour (Time series)
```sql
SELECT
  date_trunc('hour', received_at) AS time,
  COUNT(*) AS requests
FROM requests
WHERE received_at >= NOW() - INTERVAL '24 hours'
GROUP BY time
ORDER BY time
```

### Top endpoints (Table)
```sql
SELECT uri, COUNT(*) as hits
FROM requests
WHERE received_at >= NOW() - INTERVAL '24 hours'
GROUP BY uri
ORDER BY hits DESC
LIMIT 20
```

### Requests by device (Pie chart)
```sql
SELECT
  COALESCE(device_id, 'unknown') as device,
  COUNT(*) as requests
FROM requests
WHERE received_at >= NOW() - INTERVAL '24 hours'
GROUP BY device_id
ORDER BY requests DESC
LIMIT 10
```

### Requests per domain (Bar chart)
```sql
SELECT host, COUNT(*) as requests
FROM requests
WHERE received_at >= NOW() - INTERVAL '24 hours'
GROUP BY host
ORDER BY requests DESC
```

### Traffic over time by method (Time series)
```sql
SELECT
  date_trunc('hour', received_at) AS time,
  method,
  COUNT(*) AS requests
FROM requests
WHERE received_at >= NOW() - INTERVAL '24 hours'
GROUP BY time, method
ORDER BY time
```
