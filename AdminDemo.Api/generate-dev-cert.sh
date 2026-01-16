#!/bin/bash
# Script na vytvorenie development HTTPS certifikátu

CERT_DIR="./https"
mkdir -p "$CERT_DIR"

# Vytvor self-signed certifikát
openssl req -x509 -newkey rsa:4096 -nodes \
    -keyout "$CERT_DIR/dev-key.pem" \
    -out "$CERT_DIR/dev-cert.pem" \
    -days 365 \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,DNS:*.localhost,IP:127.0.0.1,IP:::1"

# Konvertuj do PKCS#12 formátu (.pfx) pre ASP.NET Core
openssl pkcs12 -export \
    -out "$CERT_DIR/dev-cert.pfx" \
    -inkey "$CERT_DIR/dev-key.pem" \
    -in "$CERT_DIR/dev-cert.pem" \
    -passout pass: \
    -name "localhost"

echo "✅ Development certifikát vytvorený v $CERT_DIR/"
echo "   - dev-cert.pem (certifikát)"
echo "   - dev-key.pem (súkromný kľúč)"
echo "   - dev-cert.pfx (PKCS#12 pre ASP.NET Core)"
