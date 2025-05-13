#!/bin/sh
[ -d "./data" ] || mkdir "./data"

# Check if any arguments were passed
if [ "$#" -eq 0 ]; then
  echo "Usage: $0 domain1 domain2 ..."
  exit 1
fi

# Assign the arguments to an array
domains=("$@")
# Join the domains with a space as the separator
domain_list=$(IFS=' '; echo "${domains[*]}")

# Create the data directory if it doesn't exist
[ -d "./data" ] || mkdir "./data"

# Check if the certbot directory doesn't exist
if ! [ -d "./data/certbot" ]; then
  cd ./letsencrypt
  /bin/bash ./init-letsencrypt.sh "$domain_list"
  mv ./data/certbot/ ../data/certbot/
    echo "letsencrypt started"
  cd ../
fi

if ! [ -f "./data/nginx/app.conf" ]; then
  [ -d "./data/nginx" ] || mkdir "./data/nginx"
  # Replace the placeholder with the joined domain list
  sed "s/example.org/$domain_list/" ./nginx/app.template.conf > ./data/nginx/app.conf
  echo "Nginx config created"
fi


exit 0