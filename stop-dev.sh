#!/bin/bash

# Script to stop Be Demo API Docker containers

echo "🛑 Stopping Be Demo API containers..."

docker-compose -f docker-compose.dev.yml down

echo "✅ Containers stopped and removed"
