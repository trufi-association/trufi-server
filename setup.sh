#!/bin/bash
set -e

CONFIG_FILE="data/config/appsettings.json"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check for jq
check_dependencies() {
    if ! command -v jq &> /dev/null; then
        echo -e "${RED}Error: jq is required but not installed.${NC}"
        echo "Install it with:"
        echo "  macOS: brew install jq"
        echo "  Ubuntu: sudo apt install jq"
        exit 1
    fi
}

# Print header
header() {
    echo -e "\n${BLUE}Trufi Server Setup${NC}"
    echo "=================="
    echo ""
}

# Check if initial setup is done
is_configured() {
    if [ -f "$CONFIG_FILE" ]; then
        local email=$(jq -r '.LettuceEncrypt.EmailAddress // ""' "$CONFIG_FILE" 2>/dev/null)
        [ -n "$email" ] && [ "$email" != "" ]
    else
        return 1
    fi
}

# Initial setup
initial_setup() {
    echo -e "${YELLOW}Initial Setup${NC}"
    echo "--------------"
    echo ""

    read -p "Email for Let's Encrypt SSL certificates: " email

    if [ -z "$email" ]; then
        echo -e "${RED}Error: Email is required for SSL certificates.${NC}"
        exit 1
    fi

    # Create config directory if needed
    mkdir -p "$(dirname "$CONFIG_FILE")"

    # Generate appsettings.json
    cat > "$CONFIG_FILE" << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Yarp": "Information"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:80"
      },
      "Https": {
        "Url": "https://*:443"
      }
    }
  },
  "LettuceEncrypt": {
    "AcceptTermsOfService": true,
    "DomainNames": [],
    "EmailAddress": "$email"
  },
  "ReverseProxy": {
    "Routes": {},
    "Clusters": {}
  }
}
EOF

    echo ""
    echo -e "${GREEN}✓ Configuration created!${NC}"
    echo ""
    echo "Next steps:"
    echo "  1. Add services with: ./setup.sh"
    echo "  2. Run: docker compose up -d"
}

# Add a service from external repo
add_external_service() {
    echo -e "${YELLOW}Add External Service${NC}"
    echo "--------------------"
    echo ""

    read -p "Path to service repo (e.g., ../trufi-server-photon): " repo_path

    if [ ! -d "$repo_path" ]; then
        echo -e "${RED}Error: Directory not found: $repo_path${NC}"
        return 1
    fi

    local proxy_file="$repo_path/trufi-proxy.json"
    if [ ! -f "$proxy_file" ]; then
        echo -e "${RED}Error: trufi-proxy.json not found in $repo_path${NC}"
        echo "See trufi-proxy.example.json for the expected format."
        return 1
    fi

    # Read service config
    local name=$(jq -r '.name // ""' "$proxy_file")
    local description=$(jq -r '.description // ""' "$proxy_file")
    local port=$(jq -r '.port // ""' "$proxy_file")
    local container=$(jq -r '.container // ""' "$proxy_file")

    # Validate required fields
    if [ -z "$name" ] || [ "$name" == "null" ]; then
        echo -e "${RED}Error: 'name' is required in trufi-proxy.json${NC}"
        return 1
    fi

    if [ -z "$port" ] || [ "$port" == "null" ]; then
        echo -e "${RED}Error: 'port' is required in trufi-proxy.json${NC}"
        return 1
    fi

    if ! [[ "$port" =~ ^[0-9]+$ ]]; then
        echo -e "${RED}Error: 'port' must be a number (got: $port)${NC}"
        return 1
    fi

    if [ -z "$container" ] || [ "$container" == "null" ]; then
        echo -e "${RED}Error: 'container' is required in trufi-proxy.json${NC}"
        return 1
    fi

    echo ""
    echo -e "${GREEN}✓ Detected: $name${NC}"
    [ -n "$description" ] && echo "  $description"
    echo "  Container: $container"
    echo "  Port: $port"
    echo ""

    read -p "Full domain for this service (e.g., photon.trufi.app): " full_domain

    if [ -z "$full_domain" ]; then
        echo -e "${RED}Error: Domain is required.${NC}"
        return 1
    fi

    add_service_to_config "$name" "$full_domain" "$port" "$container"
}

# Add analytics API (internal service)
add_analytics_service() {
    echo -e "${YELLOW}Expose Analytics API${NC}"
    echo "--------------------"
    echo ""

    read -p "Domain for Analytics API (e.g., analytics.trufi.app): " full_domain

    if [ -z "$full_domain" ]; then
        echo -e "${RED}Error: Domain is required.${NC}"
        return 1
    fi

    local name="analytics"

    # Check if already exists
    if jq -e ".ReverseProxy.Routes[\"$name\"]" "$CONFIG_FILE" > /dev/null 2>&1; then
        echo -e "${YELLOW}Warning: Analytics API already exposed. Updating...${NC}"
    fi

    local temp_file=$(mktemp)

    # Add domain to DomainNames if not exists
    jq --arg domain "$full_domain" '
        if (.LettuceEncrypt.DomainNames | index($domain)) == null then
            .LettuceEncrypt.DomainNames += [$domain]
        else
            .
        end
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    # Add route for analytics (internal, same container)
    jq --arg name "$name" --arg domain "$full_domain" '
        .ReverseProxy.Routes[$name] = {
            "ClusterId": $name,
            "Match": { "Hosts": [$domain] }
        }
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    # Add cluster pointing to localhost (same container)
    jq --arg name "$name" '
        .ReverseProxy.Clusters[$name] = {
            "Destinations": {
                "primary": { "Address": "http://localhost:80" }
            }
        }
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    echo ""
    echo -e "${GREEN}✓ Analytics API exposed!${NC}"
    echo ""
    echo "Domain: $full_domain"
    echo "Endpoints: /analytics-api/logs, /analytics-api/stats, etc."
    echo ""
    echo "Run: docker compose restart server"
}

# Add service to config (shared logic)
add_service_to_config() {
    local name="$1"
    local full_domain="$2"
    local port="$3"
    local container="${4:-$name}"  # Default container name = service name

    # Check if service already exists
    if jq -e ".ReverseProxy.Routes[\"$name\"]" "$CONFIG_FILE" > /dev/null 2>&1; then
        echo -e "${YELLOW}Warning: Service '$name' already exists. Updating...${NC}"
    fi

    # Update appsettings.json
    local temp_file=$(mktemp)

    # Add domain to DomainNames if not exists
    jq --arg domain "$full_domain" '
        if (.LettuceEncrypt.DomainNames | index($domain)) == null then
            .LettuceEncrypt.DomainNames += [$domain]
        else
            .
        end
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    # Add route
    jq --arg name "$name" --arg domain "$full_domain" '
        .ReverseProxy.Routes[$name] = {
            "ClusterId": $name,
            "Match": { "Hosts": [$domain] }
        }
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    # Add cluster
    jq --arg name "$name" --arg container "$container" --arg port "$port" '
        .ReverseProxy.Clusters[$name] = {
            "Destinations": {
                "primary": { "Address": "http://\($container):\($port)" }
            }
        }
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    echo ""
    echo -e "${GREEN}✓ Service added!${NC}"
    echo ""
    echo "Added to appsettings.json:"
    echo "  - Domain: $full_domain"
    echo "  - Backend: http://$container:$port"
    echo ""
    echo -e "${YELLOW}Remember:${NC}"
    echo "  1. Ensure '$name' service uses network 'trufi-server'"
    echo "  2. Run: docker compose restart server"
}

# List services
list_services() {
    echo -e "${YELLOW}Configured Services${NC}"
    echo "-------------------"
    echo ""

    local routes=$(jq -r '.ReverseProxy.Routes | keys[]' "$CONFIG_FILE" 2>/dev/null)

    if [ -z "$routes" ]; then
        echo "No services configured yet."
        return
    fi

    for route in $routes; do
        local hosts=$(jq -r ".ReverseProxy.Routes[\"$route\"].Match.Hosts[0] // \"\"" "$CONFIG_FILE")
        local address=$(jq -r ".ReverseProxy.Clusters[\"$route\"].Destinations.primary.Address // \"\"" "$CONFIG_FILE")
        echo -e "  ${GREEN}$route${NC}"
        echo "    Domain: $hosts"
        echo "    Backend: $address"
        echo ""
    done
}

# Remove service
remove_service() {
    echo -e "${YELLOW}Remove Service${NC}"
    echo "--------------"
    echo ""

    local routes=$(jq -r '.ReverseProxy.Routes | keys[]' "$CONFIG_FILE" 2>/dev/null)

    if [ -z "$routes" ]; then
        echo "No services configured."
        return
    fi

    echo "Configured services:"
    local i=1
    for route in $routes; do
        echo "  [$i] $route"
        i=$((i + 1))
    done
    echo ""

    read -p "Service name to remove: " name

    if [ -z "$name" ]; then
        echo "Cancelled."
        return
    fi

    # Get domain before removing
    local domain=$(jq -r ".ReverseProxy.Routes[\"$name\"].Match.Hosts[0] // \"\"" "$CONFIG_FILE")

    if [ -z "$domain" ] || [ "$domain" == "null" ]; then
        echo -e "${RED}Error: Service '$name' not found.${NC}"
        return 1
    fi

    local temp_file=$(mktemp)

    # Remove domain from DomainNames
    jq --arg domain "$domain" '
        .LettuceEncrypt.DomainNames = (.LettuceEncrypt.DomainNames | map(select(. != $domain)))
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    # Remove route
    jq --arg name "$name" '
        del(.ReverseProxy.Routes[$name])
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    # Remove cluster
    jq --arg name "$name" '
        del(.ReverseProxy.Clusters[$name])
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    echo ""
    echo -e "${GREEN}✓ Service '$name' removed.${NC}"
    echo ""
    echo "Run: docker compose restart server"
}

# Change email
change_email() {
    echo -e "${YELLOW}Change Email${NC}"
    echo "------------"
    echo ""

    local current_email=$(jq -r '.LettuceEncrypt.EmailAddress' "$CONFIG_FILE")
    echo "Current email: $current_email"
    echo ""

    read -p "New email: " new_email

    if [ -z "$new_email" ]; then
        echo "Cancelled."
        return
    fi

    local temp_file=$(mktemp)
    jq --arg email "$new_email" '
        .LettuceEncrypt.EmailAddress = $email
    ' "$CONFIG_FILE" > "$temp_file" && mv "$temp_file" "$CONFIG_FILE"

    echo ""
    echo -e "${GREEN}✓ Email updated.${NC}"
}

# Main menu
main_menu() {
    while true; do
        header

        echo "[1] Add external service (from repo with trufi-proxy.json)"
        echo "[2] Expose Analytics API"
        echo "[3] List services"
        echo "[4] Remove service"
        echo "[5] Change email"
        echo "[0] Exit"
        echo ""

        read -p "> " choice

        case $choice in
            1) add_external_service ;;
            2) add_analytics_service ;;
            3) list_services ;;
            4) remove_service ;;
            5) change_email ;;
            0) echo "Bye!"; exit 0 ;;
            *) echo "Invalid option" ;;
        esac

        echo ""
        read -p "Press Enter to continue..."
    done
}

# Main
main() {
    cd "$SCRIPT_DIR"
    check_dependencies

    if ! is_configured; then
        header
        initial_setup
    else
        main_menu
    fi
}

main
