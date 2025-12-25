#!/bin/bash

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() {
  echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
  echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
  echo -e "${RED}[ERROR]${NC} $1"
}

log_action() {
  echo -e "${BLUE}[ACTION]${NC} $1"
}

show_help() {
  echo "Trufi Server - Domain Management"
  echo ""
  echo "Usage: $0 [OPTIONS] domain1 domain2 ..."
  echo ""
  echo "Options:"
  echo "  -h, --help     Show this help message"
  echo "  -l, --list     List currently configured domains"
  echo "  -r, --remove   Remove specified domains"
  echo "  --reset        Remove ALL domains and start fresh"
  echo ""
  echo "Examples:"
  echo "  $0 api.example.com                 # Add a new domain"
  echo "  $0 api.example.com app.example.com # Add multiple domains"
  echo "  $0 -r old.example.com              # Remove a domain"
  echo "  $0 -l                              # List all domains"
  echo "  $0 --reset                         # Remove everything"
}

list_domains() {
  echo "Currently configured domains:"
  if [ -d "./data/nginx" ] && [ "$(ls -A ./data/nginx/*.conf 2>/dev/null)" ]; then
    for conf in ./data/nginx/*.conf; do
      domain=$(basename "$conf" .conf)
      echo "  - $domain"
    done
  else
    echo "  (none)"
  fi
}

remove_domains() {
  local domains_to_remove=("$@")

  for domain in "${domains_to_remove[@]}"; do
    if [ -f "./data/nginx/${domain}.conf" ]; then
      rm -f "./data/nginx/${domain}.conf"
      log_info "Removed Nginx config for $domain"
    else
      log_warn "Domain $domain not found, skipping"
    fi

    # Remove certificate if exists
    if [ -d "./data/certbot/conf/live/${domain}" ]; then
      rm -rf "./data/certbot/conf/live/${domain}"
      rm -rf "./data/certbot/conf/archive/${domain}"
      rm -f "./data/certbot/conf/renewal/${domain}.conf"
      log_info "Removed certificates for $domain"
    fi
  done

  log_info "Removal complete. Restart nginx: docker compose restart nginx_single_server"
}

reset_all() {
  log_warn "This will remove ALL domain configurations!"
  read -p "Are you sure? (y/N): " confirm
  if [ "$confirm" != "y" ] && [ "$confirm" != "Y" ]; then
    echo "Cancelled."
    exit 0
  fi

  rm -rf ./data/nginx/*.conf 2>/dev/null || true
  rm -rf ./data/certbot/conf/live/* 2>/dev/null || true
  rm -rf ./data/certbot/conf/archive/* 2>/dev/null || true
  rm -rf ./data/certbot/conf/renewal/* 2>/dev/null || true

  log_info "All domains removed"
}

# Check for required commands
check_requirements() {
  if ! command -v docker &> /dev/null; then
    log_error "Docker is not installed. Please install Docker first."
    exit 1
  fi
}

# Parse arguments
REMOVE_MODE=false
domains=()

while [[ $# -gt 0 ]]; do
  case $1 in
    -h|--help)
      show_help
      exit 0
      ;;
    -l|--list)
      list_domains
      exit 0
      ;;
    -r|--remove)
      REMOVE_MODE=true
      shift
      ;;
    --reset)
      reset_all
      exit 0
      ;;
    -*)
      log_error "Unknown option: $1"
      show_help
      exit 1
      ;;
    *)
      domains+=("$1")
      shift
      ;;
  esac
done

# Validate input arguments
if [ ${#domains[@]} -eq 0 ]; then
  log_error "No domains provided"
  show_help
  exit 1
fi

check_requirements

# Handle remove mode
if [ "$REMOVE_MODE" = true ]; then
  remove_domains "${domains[@]}"
  exit 0
fi

# Add/Update domains
log_info "Processing domains: ${domains[*]}"

# Ensure directories exist
mkdir -p "./data/nginx"
mkdir -p "./data/certbot"
mkdir -p "./data/logs"
mkdir -p "./data/static_files"
mkdir -p "./data/well-known"

# Process each domain
for domain in "${domains[@]}"; do
  # Check if domain already exists
  if [ -f "./data/nginx/${domain}.conf" ]; then
    log_action "Updating existing domain: $domain"
  else
    log_action "Adding new domain: $domain"
  fi

  # Check if certificate already exists
  if [ -d "./data/certbot/conf/live/${domain}" ]; then
    log_info "Certificate for $domain already exists, skipping Let's Encrypt"
  else
    log_info "Initializing Let's Encrypt for $domain..."
    cd ./letsencrypt
    rm -rf "./data/certbot"
    /bin/bash ./init-letsencrypt.sh "$domain"
    cp -rn ./data/certbot/* ../data/certbot/ 2>/dev/null || true
    cd ../
    log_info "Let's Encrypt initialized for $domain"
  fi

  # Generate/update nginx config
  sed "s/example.org/$domain/g" ./nginx/app.template.conf > "./data/nginx/${domain}.conf"
  log_info "Nginx config created: ${domain}.conf"
done

echo ""
log_info "Initialization complete!"
echo ""
list_domains
echo ""
echo "Next steps:"
echo "  1. Start the server: docker compose up -d"
echo "  2. Check logs: docker compose logs -f"
echo "  3. Verify health: curl http://${domains[0]}/healthcheck"

exit 0
