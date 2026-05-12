#!/bin/bash

# Script to stop Many Faces API Docker containers
# Usage: ./stop-dev.sh

set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
echo "🛑 Stopping Many Faces API containers..."

docker-compose -f docker-compose.dev.yml stop
docker-compose -f docker-compose.dev.yml rm -f

echo "✅ Many Faces API containers stopped and removed"
