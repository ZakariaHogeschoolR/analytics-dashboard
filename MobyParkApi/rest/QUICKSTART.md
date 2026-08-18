# Quick Start Guide - REST Files

## Probleem oplossen: Bestanden werken niet

### Stap 1: Controleer of de API draait
```bash
# Test of de API bereikbaar is
curl http://localhost:5082/api/parking-lots
```

Als dit niet werkt, start de API:
```bash
cd MobyParkApi
dotnet run
```

### Stap 2: Test een eenvoudige request
Open `auth.rest` en test de register request:
1. Klik op "Send Request" boven de register request
2. Check of je een response krijgt

### Stap 3: Login en token kopiëren
1. Run de login request in `auth.rest`
2. In de response zie je:
```json
{
  "message": "Inloggen succesvol!",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "role": "User"
}
```
3. Kopieer het `accessToken` (het hele lange token)

### Stap 4: Token gebruiken
**Optie A: Direct in bestand**
- Open het bestand waar je het token nodig hebt (bijv. `parking-lots.rest`)
- Zoek naar `@token = ` bovenaan
- Plak het token: `@token = eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`

**Optie B: Environment file**
- Open `.http-client.env.json`
- Plak het token in het `token` veld:
```json
{
  "development": {
    "token": "PLAK_HIER_HET_TOKEN"
  }
}
```

### Stap 5: Test een authenticated request
- Open `parking-lots.rest`
- Run de "GET SESSIONS" request
- Deze zou nu moeten werken met je token

## Veelvoorkomende problemen

### "Connection refused" of "Cannot connect"
- **Oplossing:** Start de API met `dotnet run` in de MobyParkApi directory

### "401 Unauthorized"
- **Oplossing:** Check of je token correct is gekopieerd (geen spaties, volledig token)
- **Oplossing:** Token kan verlopen zijn (log opnieuw in)

### "404 Not Found"
- **Oplossing:** Check of de endpoint URL correct is
- **Oplossing:** Check of de API op de juiste poort draait (5082)

### Variabelen werken niet
- **Oplossing:** Gebruik handmatig token kopiëren in plaats van automatische opslag
- **Oplossing:** Check of REST Client extension geïnstalleerd is

## Test volgorde

1. ✅ `auth.rest` - Register (zonder token)
2. ✅ `auth.rest` - Login (zonder token, kopieer token)
3. ✅ `parking-lots.rest` - GET ALL (zonder token, zou moeten werken)
4. ✅ `parking-lots.rest` - GET SESSIONS (met token)
5. ✅ Andere endpoints testen

## Hulp nodig?

- Check de README.md voor uitgebreide documentatie
- Test eerst eenvoudige endpoints zonder token
- Gebruik handmatig token kopiëren als automatische opslag niet werkt

