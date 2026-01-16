# Inštalácia dôveryhodného HTTPS certifikátu

Pre odstránenie varovaní o bezpečnosti v prehliadači je potrebné nainštalovať mkcert CA do systémového trust store.

## Rýchla inštalácia

Spustite tento príkaz v termináli:

```bash
mkcert -install
```

Tento príkaz:
- Vytvorí lokálny CA (Certificate Authority) certifikát
- Pridá ho do macOS keychain ako dôveryhodný root certifikát
- Umožní všetkým certifikátom vytvoreným pomocou `mkcert` byť automaticky dôveryhodnými

## Po inštalácii

1. **Reštartujte prehliadač** (alebo všetky okná)
2. Obnovte stránku `https://localhost:8001/swagger`
3. Varovanie by už nemalo byť viditeľné ✅

## Overenie

Po inštalácii mkcert CA, všetky certifikáty vytvorené pomocou `mkcert` budú automaticky dôveryhodné v:
- ✅ Chrome
- ✅ Safari  
- ✅ Firefox
- ✅ Edge
- ✅ Všetkých ostatných prehliadačoch

## Alternatíva (ak nemôžete použiť sudo)

Ak nemôžete spustiť `mkcert -install`, môžete manuálne pridať certifikát:

1. Otvorte Keychain Access
2. Drag & drop súbor: `AdminDemo.Api/https/dev-cert.pem`
3. Dvojklik na certifikát → Trust → Always Trust

**Poznámka:** mkcert je lepšie riešenie, pretože vytvára lokálny CA, ktorý môže podpisovať viacero certifikátov.
