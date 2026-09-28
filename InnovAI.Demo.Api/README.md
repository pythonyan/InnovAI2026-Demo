# InnovAI Demo API

API demo per la gestione delle segnalazioni via email con analisi AI (Semantic Kernel + Azure OpenAI).

## Prerequisiti

- .NET 8 SDK
- SQL Server 2022+ (supporto vettori) **oppure** Azure SQL con vector search abilitato
- Accesso a un'istanza Azure OpenAI con deployment `gpt-4o` e `text-embedding-3-small`

---

## Setup credenziali (User Secrets)

Le credenziali **non sono nel repository**. Vanno configurate con `dotnet user-secrets`.

### 1. Copiare il file di esempio

```
secrets.example.json
```

Contiene tutti i valori richiesti con placeholder descrittivi.

### 2. Impostare i secrets

Dalla cartella del progetto (`InnovAI.Demo.Api/`):

```bash
dotnet user-secrets set "EmailUsername"        "mittente@dominio.it"
dotnet user-secrets set "EmailPassword"        "password-smtp"
dotnet user-secrets set "EmailSmtpServer"      "smtps.aruba.it"
dotnet user-secrets set "EmailSmtpServerPort"  "465"

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:..."

dotnet user-secrets set "AzureOpenAI:ChatCompletion:Deployment"  "gpt-4o"
dotnet user-secrets set "AzureOpenAI:ChatCompletion:Endpoint"    "https://<risorsa>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ChatCompletion:ApiKey"      "<api-key>"
dotnet user-secrets set "AzureOpenAI:ChatCompletion:ModelId"     "gpt-4o"

dotnet user-secrets set "AzureOpenAI:Embedding:Deployment"  "text-embedding-3-small"
dotnet user-secrets set "AzureOpenAI:Embedding:Endpoint"    "https://<risorsa>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:Embedding:ApiKey"      "<api-key>"
dotnet user-secrets set "AzureOpenAI:Embedding:ModelId"     "text-embedding-3-small"
dotnet user-secrets set "AzureOpenAI:Embedding:Dimensions"  "1536"

dotnet user-secrets set "AzureAiService:endpointServiceTextAnalisys"  "https://<risorsa>.openai.azure.com/"
dotnet user-secrets set "AzureAiService:apiKeyServiceTextAnalisys"    "<api-key>"

dotnet user-secrets set "CrmApi:BaseUrl"    "https://<crm-host>/restapi/v1/vtews/query"
dotnet user-secrets set "CrmApi:Username"   "<utente>"
dotnet user-secrets set "CrmApi:Password"   "<password>"
```

> In produzione sostituire i User Secrets con **variabili d'ambiente** o **Azure Key Vault**.

---

## Database

| Scenario | Stringa di connessione | EnsureDbCreated |
|----------|------------------------|-----------------|
| **Demo / Azure** | DB Azure del cliente (in User Secrets) | `false` (default) |
| **Sviluppo locale** | LocalDB (in User Secrets) | `true` (in `appsettings.Development.json`) |

Per creare il DB in locale basta avviare il progetto con `ASPNETCORE_ENVIRONMENT=Development`: il flag `AppSettings:EnsureDbCreated` è già impostato a `true` in `appsettings.Development.json`.

---

## Avvio

### Avvio rapido per la demo (consigliato)

Dalla cartella `Demo/` esegui lo script PowerShell che avvia API e ngrok in un unico passaggio:

```powershell
.\start-demo.ps1
```

Lo script:
1. Avvia `dotnet run` in una finestra separata
2. Attende che la porta 7100 risponda (max 30 secondi)
3. Avvia `ngrok start --all` in un'altra finestra
4. Mostra i due URL (Swagger locale + endpoint ngrok pubblico)
5. Alla pressione di INVIO apre Swagger nel browser

> Se PowerShell blocca l'esecuzione per policy, esegui prima:
> ```powershell
> Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
> ```

### Avvio container per dashboard Aspire
```bash
docker run -d --name aspire-dashboard `
  -p 18888:18888 `
  -p 4317:4317 `
  -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
  mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

### Avvio manuale

```bash
dotnet run
```

Swagger UI disponibile su: `https://localhost:7100/swagger`  
Dashboard Hangfire: `https://localhost:7100/jobs`  
Health check: `https://localhost:7100/health`  
Dashboard UI: apri `../index.html` nel browser (API URL preimpostato a `https://localhost:7100`)

---

## Personalizzazione ente (senza codice)

Tutte le impostazioni dell'ente demo si trovano nella sezione `Demo` di `appsettings.json`:

| Chiave | Descrizione |
|--------|-------------|
| `CodiceEnte` | Codice dell'ente |
| `NomeEnte` | Nome visualizzato nelle email |
| `EmailAssistenzaEnte` | Mittente email di assistenza |
| `EmailDiContattoHelpDesk` | Destinatario email helpdesk |
| `ListaTipologiaErrore` | Lista dei tipi di segnalazione |
| `ApiKey` | API Key per l'endpoint `/api/similarity` (Copilot Studio) |
| `UseMockCrm` | `true` = stub demo con 3 ticket hardcoded · `false` = CRM reale (default: `true`) |

---

## Integrazione CRM reale

### Switch mock / reale

La chiave `Demo:UseMockCrm` in `appsettings.json` controlla quale implementazione viene usata:

```json
"Demo": {
  "UseMockCrm": false
}
```

| Valore | Comportamento |
|---|---|
| `true` (default) | `DemoCrmProxyService` — 3 ticket hardcoded, risponde sempre |
| `false` | `CrmProxyService` — chiama le API REST del CRM reale |

### Configurazione CRM reale

Imposta le credenziali tramite user-secrets (mai in `appsettings.json`):

```bash
dotnet user-secrets set "CrmApi:BaseUrl"   "https://<crm-host>/restapi/v1/vtews/query"
dotnet user-secrets set "CrmApi:Username"  "<utente-webservice>"
dotnet user-secrets set "CrmApi:Password"  "<password-webservice>"
```

Parametri opzionali in `appsettings.json` (valori di default già impostati):

| Chiave | Default | Descrizione |
|---|---|---|
| `CrmApi:EmailQueryParam` | `email` | Nome del query-param per la ricerca per email |
| `CrmApi:KeywordsQueryParam` | `keywords` | Nome del query-param per la ricerca per keyword |
| `CrmApi:TimeoutSeconds` | `10` | Timeout HTTP verso il CRM in secondi |

### Come funziona la chiamata

`CrmProxyService` effettua una `GET` autenticata con **Basic Auth**:

```
GET {BaseUrl}?email=<email>
GET {BaseUrl}?keywords=<parole-chiave>
Authorization: Basic base64(username:password)
```

La risposta deve avere questa struttura JSON (compatibile con il modello `CrmDataResponse`):

```json
{
  "status": 200,
  "data": [
    {
      "Ticket_No": "TT12345",
      "Ticket_Title": "Titolo del ticket",
      "Solution": "Soluzione documentata",
      "TicketStatus": "Closed"
    }
  ]
}
```

> Se il CRM espone un formato diverso (es. token auth, POST, campo `results` invece di `data`),
> adatta `CrmProxyService.cs` e `CrmApiSettings.cs` senza modificare il resto della pipeline.

---

## Esposizione via ngrok per Copilot Studio

L'endpoint `POST /api/similarity` è progettato per essere chiamato da Copilot Studio come HTTP Action.
Per renderlo raggiungibile dall'esterno durante sviluppo e demo si usa **ngrok**.

### 1. Avvia l'API in locale

Usa il profilo HTTP semplice (ngrok funziona meglio senza HTTPS locale):

```bash
dotnet run
```

L'API parte su `https://localhost:7100`.

### 2. Avvia ngrok

```bash
ngrok http 7100 oppure ngrok start --all (se si ha la configurazione in ngrok.yml)
```

ngrok mostra un URL pubblico tipo `https://xxxx-xx-xx-xx-xx.ngrok-free.app`. Copialo.

> Se ngrok non è installato: `winget install ngrok` oppure scaricalo da ngrok.com/download.

### 3. Verifica l'endpoint

Testa prima di configurare Copilot Studio (sostituisci `TUA-API-KEY` con il valore di `Demo:ApiKey`):

```bash
curl -X POST https://XXXX.ngrok-free.app/api/similarity \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: TUA-API-KEY" \
  -d '{"Testo":"non riesco ad autenticarmi con SPID","TopN":3}'
```

Risposta attesa:

```json
{
  "tickets": [
    { "ticketId": "TT099443", "titolo": "...", "soluzione": "...", "similarity": 0.87 }
  ]
}
```

### 4. Configura la HTTP Action in Copilot Studio

| Campo | Valore |
|---|---|
| Method | `POST` |
| URL | `https://XXXX.ngrok-free.app/api/similarity` |
| Body type | JSON |
| Body | `{"Testo": "<<descrizione dal topic>>", "TopN": 3}` |
| Header | `Content-Type: application/json` |
| Header | `X-Api-Key: TUA-API-KEY` |

**Mapping della risposta** — la proprietà radice è `tickets` (array). Ogni elemento contiene:

| Proprietà | Tipo | Descrizione |
|---|---|---|
| `ticketId` | stringa | Numero ticket CRM |
| `titolo` | stringa | Titolo / descrizione sintetica |
| `soluzione` | stringa (nullable) | Soluzione documentata nel CRM |
| `similarity` | numero | Score di similarità coseno (0–1) |

### Note

- **URL ngrok cambia a ogni riavvio** (tier gratuito): aggiorna l'URL nella HTTP Action di Copilot Studio a ogni sessione. Con dominio fisso: `ngrok http 5100 --domain=tuo-dominio.ngrok-free.app`.
- L'API deve rimanere in esecuzione durante l'intera sessione di demo.
- L'autenticazione accetta sia `X-Api-Key: {key}` sia `Authorization: Bearer {key}`.
