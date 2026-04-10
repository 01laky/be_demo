# Installing Trusted HTTPS Certificate

To remove security warnings in the browser, you need to install mkcert CA into the system trust store.

## Quick Installation

Run this command in the terminal:

```bash
mkcert -install
```

This command will:

- Create a local CA (Certificate Authority) certificate
- Add it to macOS keychain as a trusted root certificate
- Allow all certificates created using `mkcert` to be automatically trusted

## After Installation

1. **Restart your browser** (or all windows)
2. Refresh the page `https://localhost:8001/swagger`
3. The warning should no longer be visible ✅

## Verification

After installing mkcert CA, all certificates created using `mkcert` will be automatically trusted in:

- ✅ Chrome
- ✅ Safari
- ✅ Firefox
- ✅ Edge
- ✅ All other browsers

## Alternative (if you cannot use sudo)

If you cannot run `mkcert -install`, you can manually add the certificate:

1. Open Keychain Access
2. Drag & drop the file: `BeDemo.Api/https/dev-cert.pem`
3. Double-click the certificate → Trust → Always Trust

**Note:** mkcert is a better solution because it creates a local CA that can sign multiple certificates.
