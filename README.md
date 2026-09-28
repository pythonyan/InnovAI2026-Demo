# InnovAI 2026 — Demo: AI Agent + Copilot Studio

Demo presentata alla sessione **InnovAI 2026**.

Mostra come costruire un **AI Agent** con [Microsoft Agentic Framework](https://github.com/microsoft/semantic-kernel) e [Azure OpenAI](https://azure.microsoft.com/products/ai-services/openai-service) integrato con **Microsoft Copilot Studio** tramite una HTTP Action.

Il caso d'uso è la gestione delle segnalazioni di un ente pubblico: il cittadino invia una segnalazione via email, l'agente AI cerca ticket simili nel CRM e genera una risposta personalizzata.

---

## Cosa fa la demo

```
Cittadino → email segnalazione
               ↓
         MailController (ricezione)
               ↓
       AiAgentCrmService (Semantic Kernel)
         ├── CrmTicketPlugin → cerca ticket simili nel CRM
         ├── Azure OpenAI (gpt-4o) → genera risposta
         └── Azure AI Text Analytics → analisi sentiment
               ↓
         SendEmailJob (Hangfire) → invia risposta via SMTP
```

L'endpoint `POST /api/similarity` è esposto anche per **Copilot Studio** (autenticato via API Key).

---

## Stack

| Componente | Tecnologia |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| AI Orchestration | Microsoft Agentic Framework |
| LLM | Azure OpenAI (gpt-4o) |
| Embedding + Vector Search | Azure OpenAI (text-embedding-3-large) + SQL Server 2022 |
| Text Analytics | Azure AI Language |
| Job Queue | Hangfire (in-memory) |
| CRM | stub demo oppure CRM reale via REST |
| Tunneling demo | ngrok / Dev Tunnels |

---

## Avvio rapido

```powershell
# 1. Configura i segreti (vedi secrets.example.json)
cd InnovAI.Demo.Api
dotnet user-secrets set "AzureOpenAI:ChatCompletion:ApiKey" "<api-key>"
# ... (tutti i valori di secrets.example.json)

# 2. Avvia tutto in un colpo
cd ..
.\start-demo.ps1
```

Swagger UI: `https://localhost:7100/swagger`

Per la documentazione completa di setup, configurazione e integrazione Copilot Studio vedi [`InnovAI.Demo.Api/README.md`](InnovAI.Demo.Api/README.md).

Il progetto Copilot Studio companion è disponibile su: [InnovAI2026-Demo-CopilotStudio](https://github.com/pythonyan/InnovAI2026-Demo-CopilotStudio)

---

## Struttura del repository

```
InnovAI.Demo.Api/          API ASP.NET Core
├── Controllers/           MailController, SimilarityController
├── Services/              AI Agent, Vector Search, Email, CRM proxy
├── Plugins/               CrmTicketPlugin (Microsoft Agentic Framework)
├── Stubs/                 Mock CRM e servizi opzionali
├── Settings/              Classi di configurazione tipizzata
├── Data/                  DbContext + Entity (EF Core)
├── secrets.example.json   Template credenziali (senza valori reali)
└── README.md              Documentazione dettagliata
index.html                 UI demo (client statico)
start-demo.ps1             Script avvio rapido (API + ngrok)
SETUP.md                   Checklist personalizzazione pre-demo
```

---

## Licenza

MIT — Copyright 2026 Tony Pierascenzi
