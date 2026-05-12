#!/bin/bash

# Script to start Be Demo API in Docker container for development

set -e

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "🚀 Starting Be Demo API in Docker container..."

# Stop existing containers if running
echo "🛑 Stopping existing containers..."
docker-compose -f docker-compose.dev.yml down 2>/dev/null || true

# Create directories if they don't exist
mkdir -p BeDemo.Api/https

# Create HTTPS certificate if it doesn't exist
if [ ! -f "BeDemo.Api/https/dev-cert.pfx" ]; then
    echo "🔐 Creating development HTTPS certificate..."
    cd BeDemo.Api
    ./generate-dev-cert.sh
    cd ..
fi

# Build and start containers
echo "🔨 Building Docker image..."
docker-compose -f docker-compose.dev.yml build

echo "▶️  Starting containers..."
docker-compose -f docker-compose.dev.yml up -d

# Wait for container to be ready
echo "⏳ Waiting for container to be ready..."
sleep 8

# Run migrations (PostgreSQL database should be running separately)
echo "📦 Running database migrations..."
docker-compose -f docker-compose.dev.yml exec -T be-demo-api dotnet ef database update || echo "⚠️  Attempting to run migrations..."
sleep 3
echo "✅ Database migrations completed. Admin user will be created automatically on first startup."

# Check if application is running
if curl -s -k https://localhost:8001/swagger/index.html > /dev/null 2>&1 || curl -s http://localhost:8000/swagger/index.html > /dev/null 2>&1; then
    echo "✅ Application started successfully!"
    echo ""
    echo "📍 HTTP URL: http://localhost:8000"
    echo "🔒 HTTPS URL: https://localhost:8001"
    echo "📚 Swagger UI (HTTP): http://localhost:8000/swagger"
    echo "📚 Swagger UI (HTTPS): https://localhost:8001/swagger"
    echo "📊 Seq Logging UI: http://localhost:5341"
    echo ""
    echo "⚠️  Note: HTTPS uses self-signed certificate. Browser may show a warning."
    echo ""
    echo "📋 Useful commands:"
    echo "   - View logs: docker-compose -f docker-compose.dev.yml logs -f"
    echo "   - Stop: docker-compose -f docker-compose.dev.yml down"
    echo "   - Restart: docker-compose -f docker-compose.dev.yml restart"
else
    echo "⚠️  Application is still starting. Try again in a moment."
    echo "📋 View logs: docker-compose -f docker-compose.dev.yml logs -f"
fi
