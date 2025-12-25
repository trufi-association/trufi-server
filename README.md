# Trufi Server - Vanilla

This repository enables the creation of a robust production backend environment, specifically designed to support the customized iteration of the Trufi App. It empowers users to establish and deploy their own personalized Trufi App instances.

## Architecture

<img src="./diagram/trufi-nginx.png" hspace="20"/>

## Features

- **Multi-domain support**: Configure multiple domains incrementally
- **Incremental management**: Add, update, or remove domains without affecting others
- **Automatic SSL/TLS**: Let's Encrypt certificates with auto-renewal every 12 hours
- **HTTP/2**: Enabled for better performance
- **Docker-based**: Easy deployment and management
- **Health monitoring**: Built-in health check endpoint
- **Auto-restart**: Services automatically restart on failure

## Requirements

- Docker and Docker Compose
- A server with ports 80 and 443 available
- Domain name(s) pointing to your server's IP address

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/trufi-association/trufi-server.git
cd trufi-server
```

### 2. Initialize with your domain(s)

```bash
./init.sh api.yourdomain.com
```

### 3. Start the server

```bash
docker compose up -d
```

### 4. Verify the installation

```bash
curl https://yourdomain.com/static_files/healthcheck.json
```

## Domain Management

The `init.sh` script provides full domain lifecycle management:

### Add domains

```bash
# Add a single domain
./init.sh api.example.com

# Add multiple domains at once
./init.sh api.example.com app.example.com

# Add more domains later (existing ones are preserved)
./init.sh admin.example.com
```

### List configured domains

```bash
./init.sh -l
# or
./init.sh --list
```

### Update a domain

Simply run init.sh again with the domain name. It will update the Nginx configuration without re-requesting the SSL certificate:

```bash
./init.sh api.example.com
```

### Remove domains

```bash
# Remove a specific domain
./init.sh -r old.example.com

# Remove multiple domains
./init.sh -r old1.example.com old2.example.com
```

### Reset everything

```bash
./init.sh --reset
```

### Help

```bash
./init.sh -h
# or
./init.sh --help
```

## Configuration

### SSL Certificates

SSL certificates are automatically obtained from Let's Encrypt during initialization. Certificates are renewed automatically every 12 hours.

To configure email notifications for certificate expiration, edit `letsencrypt/init-letsencrypt.sh`:

```bash
email="your-email@example.com"  # Line 19
```

### Static Files

Place your static files in `data/static_files/`. They will be served at:

```
https://yourdomain.com/static_files/
```

Directory listing is enabled, so you can browse files directly.

### Well-Known Directory

The `.well-known` directory is served at `https://yourdomain.com/.well-known/` for ACME challenges and other standard endpoints (like `assetlinks.json` for Android apps).

### Nginx Configuration

Each domain gets its own configuration file in `data/nginx/`:

The template includes these location blocks for static files and well-known:

```nginx
location /static_files/ {
    alias /app/static_files/;
    autoindex on;
}

location /.well-known/ {
    alias /app/well-known/;
    autoindex on;
}
```

```
data/nginx/
├── api.example.com.conf
├── app.example.com.conf
└── admin.example.com.conf
```

To customize a domain's configuration, edit its `.conf` file directly.

## Directory Structure

```
trufi-server/
├── docker-compose.yml          # Main Docker Compose configuration
├── init.sh                     # Domain management script
├── README.md
├── data/
│   ├── nginx/                  # Individual Nginx configs per domain
│   │   ├── domain1.conf
│   │   └── domain2.conf
│   ├── certbot/                # SSL certificates
│   │   └── conf/
│   │       ├── live/           # Current certificates
│   │       ├── archive/        # Certificate history
│   │       └── renewal/        # Renewal configs
│   ├── logs/                   # Nginx access and error logs
│   ├── static_files/           # Your static files
│   │   └── healthcheck.json    # Health check endpoint
│   └── well-known/             # .well-known directory
├── nginx/
│   └── app.template.conf       # Nginx configuration template
├── letsencrypt/
│   ├── init-letsencrypt.sh     # Certificate initialization
│   ├── app.base.conf           # Base config for ACME challenge
│   └── docker-compose.yml      # Certbot compose file
└── diagram/
    └── trufi-nginx.png         # Architecture diagram
```

## Common Operations

### Service management

```bash
# Start services
docker compose up -d

# Stop services
docker compose down

# Restart services
docker compose restart

# Restart only Nginx (after config changes)
docker compose restart nginx_single_server
```

### View logs

```bash
# All services
docker compose logs -f

# Nginx only
docker compose logs -f nginx_single_server

# Certbot only
docker compose logs -f certbot
```

### Check service health

```bash
# Container status
docker compose ps

# Health check
curl https://yourdomain.com/static_files/healthcheck.json
```

### Certificate management

```bash
# Check certificate status
docker compose exec certbot certbot certificates

# Force certificate renewal
docker compose exec certbot certbot renew --force-renewal
docker compose restart nginx_single_server
```

### Nginx configuration

```bash
# Test Nginx configuration
docker compose exec nginx_single_server nginx -t

# Reload Nginx (without restart)
docker compose exec nginx_single_server nginx -s reload
```

## Troubleshooting

### Certificate issues

```bash
# Check certificate status
docker compose exec certbot certbot certificates

# View Certbot logs
docker compose logs certbot

# Check if certificates exist
ls -la data/certbot/conf/live/
```

### Nginx not starting

```bash
# Test Nginx configuration for syntax errors
docker compose exec nginx_single_server nginx -t

# View Nginx error logs
cat data/logs/error.log

# Check if config files exist
ls -la data/nginx/
```

### Port already in use

Make sure ports 80 and 443 are not used by other services:

```bash
sudo lsof -i :80
sudo lsof -i :443

# Kill process using port (if needed)
sudo kill -9 <PID>
```

### Domain not working after adding

1. Check DNS is pointing to your server: `dig yourdomain.com`
2. Check Nginx config was created: `ls data/nginx/`
3. Check certificate exists: `ls data/certbot/conf/live/yourdomain.com/`
4. Restart Nginx: `docker compose restart nginx_single_server`

### SSL certificate not renewing

```bash
# Check renewal configuration
cat data/certbot/conf/renewal/yourdomain.com.conf

# Test renewal (dry run)
docker compose exec certbot certbot renew --dry-run

# Force renewal
docker compose exec certbot certbot renew --force-renewal
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is part of the Trufi Association initiative.
