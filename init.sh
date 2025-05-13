#!/bin/bash

# Ensure the data directory exists
[ -d "./data" ] || mkdir "./data"

# Validate input arguments
if [ "$#" -eq 0 ]; then
  echo "Usage: $0 domain1 domain2 ..."
  exit 1
fi

domains=("$@")

# Crash if ./data/certbot already exists
if [ -d "./data/certbot" ]; then
  echo "Error: ./data/certbot already exists. Aborting to avoid overwriting."
  exit 1
fi

# Create necessary directories
mkdir -p "./data/nginx"
mkdir -p "./data/certbot"

# Run init-letsencrypt.sh for each domain individually (if required by your use-case)
cd ./letsencrypt
for domain in "${domains[@]}"; do
  /bin/bash ./init-letsencrypt.sh "$domain"
  echo "letsencrypt initialized for $domain"
done
mv ./data/certbot/* ../data/certbot/
cd ../

# Generate combined nginx config
echo "# Nginx configuration for multiple domains" > ./data/nginx/app.conf
for domain in "${domains[@]}"; do
  sed "s/example.org/$domain/g" ./nginx/app.template.conf >> ./data/nginx/app.conf
  echo "Nginx config block for $domain created"
done

echo "Nginx configuration file created with multiple domains"

exit 0
