#!/bin/bash
[ -d "./data" ] || mkdir "./data"

# Check if any arguments were passed
if [ "$#" -eq 0 ]; then
  echo "Usage: $0 domain1 domain2 ..."
  exit 1
fi

# Assign the arguments to an array
domains=("$@")

# Create the data directory if it doesn't exist
[ -d "./data" ] || mkdir "./data"

# Check if the certbot directory doesn't exist
if ! [ -d "./data/certbot" ]; then
  cd ./letsencrypt
  /bin/bash ./init-letsencrypt.sh "$domains"
  mv ./data/certbot/ ../data/certbot/
    echo "letsencrypt started"
  cd ../
fi

if ! [ -f "./data/nginx/app.conf" ]; then
  [ -d "./data/nginx" ] || mkdir "./data/nginx"

  # Start creating the Nginx configuration file
  echo "# Nginx configuration for multiple domains" > ./data/nginx/app.conf
  
  for domain in "${domains[@]}"; do
    # Generate the config for each domain using the template
    sed "s/example.org/$domain/g" ./nginx/app.template.conf >> ./data/nginx/app.conf
    echo "Nginx config block for $domain created"
  done

  echo "Nginx configuration file created with multiple domains"
fi



exit 0