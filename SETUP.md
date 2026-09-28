# InnovAI Demo — Setup e personalizzazione

Progetto autoconsistente estratto da PosizioniService per la demo InnovAI.

---

## Checklist di personalizzazione (prima della demo)

### 1 — Configurazioni obbligatorie in `appsettings.json`

| Chiave | Dove trovare il valore |
|--------|----------------------|
| `AzureOpenAI.ChatCompletion.*` | Azure AI Studio → Deployments → gpt-4o |
| `AzureOpenAI.Embedding.*` | Azure AI Studio → Deployments → text-embedding-3-large |
| `DemoEnte.EmailAssistenzaEnte` | Email mittente per le mail demo |
| `DemoEnte.EmailDiContattoHelpDesk` | Email helpdesk destinataria |
| `DemoEnte.ApiKey` | Stringa segreta per autenticare `/api/similarity` da Copilot Studio |
| `EmailUsername` / `EmailPassword` | Credenziali SMTP (es. Gmail app password) |
| `EmailSmtpServer` / `EmailSmtpServerPort` | `smtp.gmail.com` / `587` oppure server aziendale |
| `ConnectionStrings.DefaultConnection` | SQL Server LocalDB (default) o connection string Azure |

> **Mai committare `appsettings.json` con secret reali.** Usare User Secrets localmente
> (`dotnet user-secrets set "EmailPassword" "xxx"`) o App Settings su Azure.

---

### 2 — Database

Il DB viene creato automaticamente al primo avvio (`EnsureCreated`).

Requisiti SQL Server:
- **SQL Server 2022+** (per `VECTOR_DISTANCE`; LocalDB 2022 su Windows è ok)
- Il tipo `VECTOR(1536)` richiede SQL Server 2022 CU4+

Per creare manualmente le migration EF:
```bash
cd InnovAI.Demo.Api
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

### 3 — Avvio in locale

```bash
cd InnovAI.Demo.Api
dotnet run
```

- Swagger UI: http://localhost:5000/swagger
- Hangfire Dashboard: http://localhost:5000/jobs
- Health check: http://localhost:5000/health

La porta è quella di default Kestrel (5000/5001). Per cambiarla: `appsettings.json` → aggiungi sezione `Kestrel`.

---

### 4 — Esposizione HTTPS pubblica (Fase 1.2)

**Opzione consigliata per la demo — Dev Tunnels:**
```bash
devtunnel host -p 5000 --allow-anonymous
```
Ottieni un URL tipo `https://xxxxx.devtunnels.ms` da usare in Copilot Studio.

**Alternativa — Azure App Service:**
```bash
dotnet publish -c Release -o ./publish
az webapp deploy --resource-group innovai-demo-rg --name innovai-demo --src-path ./publish
```

---

### 5 — Endpoint Copilot Studio (`POST /api/similarity`)

```http
POST https://{tuo-endpoint}/api/similarity
X-Api-Key: {DemoEnte:ApiKey}
Content-Type: application/json

{
  "testo": "Non riesco a vedere la multa nel portale",
  "categoria": "amministrativo",
  "topN": 3
}
```

Risposta:
```json
{
  "tickets": [
    {
      "ticketId": "TT101876",
      "titolo": "Multa non visibile dopo notifica raccomandata",
      "soluzione": "Verificare il codice fiscale...",
      "similarity": 0.92
    }
  ]
}
```

Il CRM è attualmente **stub con 3 ticket hardcoded** in `Stubs/DemoCrmProxyService.cs`.
Per usare il CRM reale: sostituire `DemoCrmProxyService` con `CrmProxyService` (da PosizioniService)
e aggiungere le credenziali `CrmApi:BaseUrl`, `CrmApi:Username`, `CrmApi:Password` in appsettings.

---

### 6 — Cattura HTML email (Fase 1.5)

Per catturare il body HTML generato prima dell'invio, aggiungere in `SendEmailJob.cs`
subito prima della riga `await client.SendAsync(message)`:

```csharp
await File.WriteAllTextAsync(
    Path.Combine(Path.GetTempPath(), $"demo-{codiceFiscalePartitaIva}-{DateTime.Now:HHmmss}.html"),
    htmlContent);
```

**Rimuovere questa riga prima del deploy pubblico.**

---

### 7 — Struttura del progetto

```
InnovAI.Demo.Api/
├── Controllers/        MailController + SimilarityController
├── Services/           SendEmailJob, AiAgentCrmService, VectorSearchService, ...
├── Stubs/              NullEntiConfigService, NullTextAnalysis, DemoCrmProxy
├── Plugins/            CrmTicketPlugin (Semantic Kernel)
├── Settings/           AiAgentCrmSettings, AzureOpenAISettings, DemoEnteSettings
├── Data/               DemoDbContext + Entities (MailData, Document, DocumentChunk)
├── Interfaces/         ISendEmailService, IAiAgentCrmService, ...
├── DTO/                RisultatoSegnalazioniSimili, CrmModels, ...
├── Filters/            ApiKeyAuthFilter (per /api/similarity)
├── ViewModels/         RichiestaSendMailViewModel, SimilarityViewModels
└── Templates/          template_assistenza.txt
```
