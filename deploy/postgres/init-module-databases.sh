#!/bin/bash
# Creates one database per module. Runs once, when the data volume is first created.
set -e
for db in identity campaigns pledges moderation notifications campaignupdates audit rosettastone; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres -c "CREATE DATABASE ${db}_db"
done
