#!/bin/bash
# Patroni post_init hook: runs once, on the first primary of a cluster.
# $1 = database to create, $2 = superuser connection URL (appended by Patroni).
set -e
psql "$2" -v ON_ERROR_STOP=1 -c "CREATE DATABASE $1"
