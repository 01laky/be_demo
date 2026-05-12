#!/bin/bash

# Script to stop Be Demo API Docker containers
# Usage: ./stop-dev.sh

set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
echo "🛑 Stopping Be Demo API containers..."

docker-compose -f docker-compose.dev.yml stop
docker-compose -f docker-compose.dev.yml rm -f

echo "✅ Be Demo API containers stopped and removed"
