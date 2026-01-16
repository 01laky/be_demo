#!/bin/bash

# Script na spustenie Admin Demo API v Docker kontajneri pre vývoj

set -e

echo "🚀 Spúšťam Admin Demo API v Docker kontajneri..."

# Zastav existujúce kontajnery ak bežia
echo "🛑 Zastavujem existujúce kontajnery..."
docker-compose -f docker-compose.dev.yml down 2>/dev/null || true

# Vytvor adresáre ak neexistujú
mkdir -p AdminDemo.Api/data
mkdir -p AdminDemo.Api/https

# Vytvor HTTPS certifikát ak neexistuje
if [ ! -f "AdminDemo.Api/https/dev-cert.pfx" ]; then
    echo "🔐 Vytváram development HTTPS certifikát..."
    cd AdminDemo.Api
    ./generate-dev-cert.sh
    cd ..
fi

# Zostav a spusti kontajnery
echo "🔨 Zostavujem Docker image..."
docker-compose -f docker-compose.dev.yml build

echo "▶️  Spúšťam kontajnery..."
docker-compose -f docker-compose.dev.yml up -d

# Počkaj kým kontajner bude pripravený
echo "⏳ Čakám na pripravenosť kontajnera..."
sleep 8

# Spusti migrations ak databáza neexistuje
if [ ! -f "AdminDemo.Api/data/AdminDemoDb.db" ]; then
    echo "📦 Vytváram databázu pomocou migrations..."
    docker-compose -f docker-compose.dev.yml exec -T admin-demo-api dotnet ef database update || echo "⚠️  Migrations sa pokúšajú spustiť..."
    sleep 3
fi

# Skontroluj či aplikácia beží
if curl -s -k https://localhost:8001/swagger/index.html > /dev/null 2>&1 || curl -s http://localhost:8000/swagger/index.html > /dev/null 2>&1; then
    echo "✅ Aplikácia úspešne spustená!"
    echo ""
    echo "📍 HTTP URL: http://localhost:8000"
    echo "🔒 HTTPS URL: https://localhost:8001"
    echo "📚 Swagger UI (HTTP): http://localhost:8000/swagger"
    echo "📚 Swagger UI (HTTPS): https://localhost:8001/swagger"
    echo ""
    echo "⚠️  Poznámka: HTTPS používa self-signed certifikát. Prehliadač môže zobraziť varovanie."
    echo ""
    echo "📋 Užitočné príkazy:"
    echo "   - Zobraziť logy: docker-compose -f docker-compose.dev.yml logs -f"
    echo "   - Zastaviť: docker-compose -f docker-compose.dev.yml down"
    echo "   - Reštartovať: docker-compose -f docker-compose.dev.yml restart"
else
    echo "⚠️  Aplikácia sa ešte spúšťa. Skúste znova za chvíľu."
    echo "📋 Zobraziť logy: docker-compose -f docker-compose.dev.yml logs -f"
fi
