#!/bin/bash
# Script to create development HTTPS certificate using mkcert

CERT_DIR="./https"
mkdir -p "$CERT_DIR"

# Check if mkcert is installed
if ! command -v mkcert &> /dev/null; then
    echo "❌ mkcert is not installed!"
    echo ""
    echo "📦 Installation:"
    echo "   brew install mkcert"
    echo ""
    echo "🔐 After installation run:"
    echo "   mkcert -install"
    echo ""
    exit 1
fi

# Check if mkcert CA is installed
if ! mkcert -CAROOT &> /dev/null; then
    echo "⚠️  mkcert CA is not installed in system trust store."
    echo ""
    echo "🔐 Run this command (requires sudo):"
    echo "   mkcert -install"
    echo ""
    echo "💡 This will create a local trusted CA certificate that will be"
    echo "   automatically recognized by all browsers without warnings."
    echo ""
    read -p "Do you want to install mkcert CA now? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        mkcert -install
        if [ $? -eq 0 ]; then
            echo "✅ mkcert CA installed successfully!"
        else
            echo "❌ Installation failed. Try manually: sudo mkcert -install"
            exit 1
        fi
    else
        echo "⚠️  Continuing without CA installation. Certificate will not be trusted."
    fi
fi

# Create certificate using mkcert
echo "🔐 Creating development certificate using mkcert..."
mkcert -key-file "$CERT_DIR/dev-key.pem" -cert-file "$CERT_DIR/dev-cert.pem" localhost 127.0.0.1 ::1

if [ $? -ne 0 ]; then
    echo "❌ Certificate creation failed!"
    exit 1
fi

# Convert to PKCS#12 format (.pfx) for ASP.NET Core
echo "🔄 Converting to PKCS#12 format..."
openssl pkcs12 -export \
    -out "$CERT_DIR/dev-cert.pfx" \
    -inkey "$CERT_DIR/dev-key.pem" \
    -in "$CERT_DIR/dev-cert.pem" \
    -passout pass: \
    -name "localhost" \
    > /dev/null 2>&1

if [ $? -eq 0 ]; then
    echo "✅ Development certificate created in $CERT_DIR/"
    echo "   - dev-cert.pem (certificate)"
    echo "   - dev-key.pem (private key)"
    echo "   - dev-cert.pfx (PKCS#12 for ASP.NET Core)"
    echo ""
    
    if mkcert -CAROOT &> /dev/null; then
        echo "✅ Certificate is trusted (mkcert CA is installed)"
        echo "   Browsers will automatically recognize it without warnings!"
    else
        echo "⚠️  Certificate is NOT trusted"
        echo "   Run: mkcert -install"
    fi
else
    echo "⚠️  Conversion to .pfx failed, but .pem files are available"
fi
