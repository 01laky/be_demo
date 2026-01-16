#!/bin/bash
# Script na vytvorenie development HTTPS certifikátu pomocou mkcert

CERT_DIR="./https"
mkdir -p "$CERT_DIR"

# Skontroluj, či je mkcert nainštalovaný
if ! command -v mkcert &> /dev/null; then
    echo "❌ mkcert nie je nainštalovaný!"
    echo ""
    echo "📦 Inštalácia:"
    echo "   brew install mkcert"
    echo ""
    echo "🔐 Po inštalácii spustite:"
    echo "   mkcert -install"
    echo ""
    exit 1
fi

# Skontroluj, či je mkcert CA nainštalovaný
if ! mkcert -CAROOT &> /dev/null; then
    echo "⚠️  mkcert CA nie je nainštalovaný v systémovom trust store."
    echo ""
    echo "🔐 Spustite tento príkaz (vyžaduje sudo):"
    echo "   mkcert -install"
    echo ""
    echo "💡 Toto vytvorí lokálne dôveryhodný CA certifikát, ktorý bude"
    echo "   automaticky uznaný všetkými prehliadačmi bez varovaní."
    echo ""
    read -p "Chcete nainštalovať mkcert CA teraz? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        mkcert -install
        if [ $? -eq 0 ]; then
            echo "✅ mkcert CA úspešne nainštalovaný!"
        else
            echo "❌ Inštalácia zlyhala. Skúste manuálne: sudo mkcert -install"
            exit 1
        fi
    else
        echo "⚠️  Pokračujem bez inštalácie CA. Certifikát nebude dôveryhodný."
    fi
fi

# Vytvor certifikát pomocou mkcert
echo "🔐 Vytváram development certifikát pomocou mkcert..."
mkcert -key-file "$CERT_DIR/dev-key.pem" -cert-file "$CERT_DIR/dev-cert.pem" localhost 127.0.0.1 ::1

if [ $? -ne 0 ]; then
    echo "❌ Vytvorenie certifikátu zlyhalo!"
    exit 1
fi

# Konvertuj do PKCS#12 formátu (.pfx) pre ASP.NET Core
echo "🔄 Konvertujem do PKCS#12 formátu..."
openssl pkcs12 -export \
    -out "$CERT_DIR/dev-cert.pfx" \
    -inkey "$CERT_DIR/dev-key.pem" \
    -in "$CERT_DIR/dev-cert.pem" \
    -passout pass: \
    -name "localhost" \
    > /dev/null 2>&1

if [ $? -eq 0 ]; then
    echo "✅ Development certifikát vytvorený v $CERT_DIR/"
    echo "   - dev-cert.pem (certifikát)"
    echo "   - dev-key.pem (súkromný kľúč)"
    echo "   - dev-cert.pfx (PKCS#12 pre ASP.NET Core)"
    echo ""
    
    if mkcert -CAROOT &> /dev/null; then
        echo "✅ Certifikát je dôveryhodný (mkcert CA je nainštalovaný)"
        echo "   Prehliadače ho budú automaticky uznávať bez varovaní!"
    else
        echo "⚠️  Certifikát NIE JE dôveryhodný"
        echo "   Spustite: mkcert -install"
    fi
else
    echo "⚠️  Konverzia do .pfx zlyhala, ale .pem súbory sú k dispozícii"
fi
