# REST Client Files - API Testing

Deze directory bevat gestructureerde `.rest` files voor het testen van alle API endpoints.

## 📁 Bestandsstructuur

```
rest/
├── README.md (dit bestand)
├── auth.rest              # Authenticatie (register/login/logout)
├── parking-lots.rest      # Parkeerplaatsen endpoints
├── vehicles.rest          # Voertuigen endpoints
├── reservations.rest      # Reserveringen endpoints
├── payments.rest          # Betalingen endpoints
├── profile.rest           # Profiel endpoints
└── users-admin.rest       # Admin gebruikersbeheer endpoints
```

## 🚀 Gebruik

### 1. Start de API
Zorg dat de API draait op `http://localhost:5082` (of pas de `@baseUrl` aan in elk bestand).

### 2. Authenticatie (EERST!)
**Start altijd met `auth.rest`:**
1. Run eerst `register` om een nieuwe gebruiker aan te maken
2. Run dan `login` om in te loggen en een token te krijgen
3. Het token wordt automatisch opgeslagen voor gebruik in andere requests

### 3. Test andere endpoints
Na authenticatie kun je de andere `.rest` files gebruiken:
- `parking-lots.rest` - Test alle parkeerplaats endpoints
- `vehicles.rest` - Test alle voertuig endpoints
- `reservations.rest` - Test alle reservering endpoints
- `payments.rest` - Test alle betaling endpoints
- `profile.rest` - Test alle profiel endpoints
- `users-admin.rest` - Test admin endpoints (vereist admin token)

## 🔑 Token Management

### Methode 1: Automatische Token Opslag (REST Client Extension)
De REST Client extension kan tokens automatisch opslaan via response scripts. 
Na het uitvoeren van de `login` request in `auth.rest`, wordt het token automatisch opgeslagen in een globale variabele.

### Methode 2: Handmatige Token Opslag (Aanbevolen)
1. Run de `login` request in `auth.rest`
2. Kopieer het `accessToken` uit de response
3. Plak het token in de `@token` variabele bovenaan het bestand waar je het nodig hebt
   - Of gebruik het `.http-client.env.json` bestand

### Token Gebruik
Alle andere `.rest` files gebruiken het token via:
```
Authorization: Bearer {{token}}
```

### Environment File
Je kunt ook het `.http-client.env.json` bestand gebruiken:
1. Kopieer `.http-client.env.json.example` naar `.http-client.env.json`
2. Plak je token in het `token` veld
3. Gebruik `{{token}}` in je requests

### Admin Token
Voor admin endpoints, log in als admin gebruiker en gebruik:
```
Authorization: Bearer {{adminToken}}
```

## 📝 Best Practices

1. **Volg de volgorde:**
   - Eerst `auth.rest` (register → login)
   - Dan andere endpoints testen

2. **Test scenarios:**
   - Elke `.rest` file bevat test scenarios onderaan
   - Deze volgen complete flows (create → get → update → delete)

3. **Error handling:**
   - Check de response status codes
   - Bekijk error messages in de response body

4. **Data dependencies:**
   - Sommige endpoints vereisen dat andere data eerst bestaat
   - Bijvoorbeeld: reserveringen vereisen een voertuig en parkeerplaats

## 🔄 End-to-End Flows

### Complete User Journey:
1. `auth.rest` - Register & Login
2. `vehicles.rest` - Voeg voertuig toe
3. `parking-lots.rest` - Bekijk parkeerplaatsen
4. `reservations.rest` - Maak reservering
5. `payments.rest` - Maak betaling
6. `profile.rest` - Update profiel

### Parking Session Flow:
1. `auth.rest` - Login
2. `vehicles.rest` - Voeg voertuig toe (of gebruik bestaand)
3. `parking-lots.rest` - Start sessie
4. `parking-lots.rest` - Stop sessie
5. `payments.rest` - Bekijk betalingen

## 🛠️ VS Code REST Client Extension

Deze files werken met de **REST Client** extension voor VS Code:
- Install: [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
- Klik op "Send Request" boven elke request om deze uit te voeren
- Responses worden getoond in een nieuw tabblad

## 📊 Response Format

Alle endpoints retourneren JSON. Voorbeelden:

**Success Response:**
```json
{
  "id": 1,
  "name": "Test",
  ...
}
```

**Error Response:**
```json
{
  "error": "Error message"
}
```

## 🔐 Authentication

- **Public endpoints:** Geen token vereist (bijv. GET parking-lots)
- **User endpoints:** Token vereist (meeste endpoints)
- **Admin endpoints:** Admin token vereist (bijv. create/update/delete parking-lots)

## 📌 Notities

- Base URL: `http://localhost:5082` (aanpasbaar per bestand)
- Content-Type: `application/json` voor alle POST/PUT/PATCH requests
- Token wordt automatisch meegestuurd via `Authorization: Bearer {{token}}`

## 🐛 Troubleshooting

### Token niet gevonden
- Zorg dat je eerst `login` hebt uitgevoerd in `auth.rest`
- Check of het token correct is opgeslagen

### 401 Unauthorized
- Check of je token nog geldig is (tokens verlopen na 1 uur)
- Log opnieuw in via `auth.rest`

### 403 Forbidden
- Check of je de juiste rol hebt (Admin voor admin endpoints)
- Gebruik admin token voor admin endpoints

### 404 Not Found
- Check of de resource bestaat (bijv. parking lot ID)
- Check of je de juiste endpoint URL gebruikt

### 400 Bad Request
- Check of alle required velden zijn ingevuld
- Check of de data format correct is (bijv. datum format)

