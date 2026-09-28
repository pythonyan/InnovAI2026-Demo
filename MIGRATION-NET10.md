# Piano di Migrazione — .NET 10 e EF Core 10

**Progetto:** Sharp4AI.Demo.Api  
**Data piano:** 2026-06-18  
**Scadenza EOL .NET 8 e .NET 9:** 10 novembre 2026  
**Riferimento Build 2026:** sessione TT656 — *From Locked-In to Liquid: Modernizing .NET Before the November 2026 EOL*

---

## Contesto — Due repo, codice identico

Il progetto vive in due repository con deploy diversi:

| Repo | Percorso locale | Visibilità |
|------|-----------------|------------|
| Repo privato | `D:\Sessioni\CFS Sharp4Ai PEscara\Demo` | Privato |
| Repo pubblico | `D:\Sessioni\Sharp4Ai Pescara\Sharp4Ai-CopilotStudio\Demo` | Pubblico (GitHub) |

**Regola:** il codice deve rimanere identico tra i due repo. Le modifiche si fanno su uno e si applicano manualmente all'altro prima di ogni commit.

---

## Stack attuale

| Componente | Versione attuale | Note |
|------------|-----------------|------|
| .NET | 8.0 | EOL 10 novembre 2026 |
| EF Core | 8.0.11 | |
| EFCore.SqlServer.VectorSearch | 0.2.0 | Plugin pre-release, non più necessario con EF 10 |
| Microsoft.SemanticKernel | 1.54.0 | |
| OpenTelemetry | 1.15.x | |
| Serilog.AspNetCore | 8.0.1 | |
| Swashbuckle.AspNetCore | 6.5.0 | |
| Hangfire | 1.8.11 | |
| SQL Server locale | LocalDB 2022 | Vincolo: vedi Fase 2 |

---

## Fase 1 — Upgrade EF Core 9 + plugin VectorSearch 9.0.0

**Quando:** subito, indipendente dalla migrazione .NET  
**Rischio:** medio — richiede una nuova migration EF (cambio tipo colonna)  
**Prerequisito SQL Server:** SQL Server 2022 CU4+ o Azure SQL (LocalDB 2022 è ok)

### 1.1 — Aggiorna i package nel `.csproj`

```xml
<!-- EF Core: da 8.0.11 a 9.0.8 -->
<PackageReference Include="EFCore.SqlServer.VectorSearch" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.8" />
```

### 1.2 — Verifica le entity con vettori

Il tipo della property embedding cambia da quello usato dalla versione 0.2.0 verso `SqlVector<float>`.
Cerca tutte le property di tipo vettoriale nel `DbContext` e nelle entity:

```bash
# Individua l'entity che usa il vettore
grep -r "VectorDistance\|Embedding\|vector" Sharp4AI.Demo.Api/ --include="*.cs" -l
```

La property embedding deve essere dichiarata con `[Column(TypeName = "vector(1536)")]`:

```csharp
[Column(TypeName = "vector(1536)")]
public SqlVector<float> Embedding { get; set; }
```

Le query LINQ di similarity search usano:

```csharp
var sqlVector = new SqlVector<float>(embeddingArray);
var results = await context.DocumentChunks
    .OrderBy(d => EF.Functions.VectorDistance("cosine", d.Embedding, sqlVector))
    .Take(topN)
    .ToListAsync();
```

### 1.3 — Crea una nuova migration EF

La colonna cambia tipo da quello del plugin 0.2.0 al tipo nativo `vector(N)`:

```bash
cd Sharp4AI.Demo.Api
dotnet ef migrations add UpgradeToNativeVector
dotnet ef database update
```

> **Attenzione:** la migration farà DROP + CREATE della colonna vettoriale perdendo i dati di embedding esistenti. I dati demo vengono ricalcolati al riavvio se il codice di seeding è attivo.

### 1.4 — Build e test

```bash
dotnet build
dotnet run
```

Verifica che `POST /api/similarity` risponda correttamente.

### 1.5 — Applica le stesse modifiche al secondo repo

Copia manualmente i file modificati in `D:\Sessioni\Sharp4Ai Pescara\Sharp4Ai-CopilotStudio\Demo`.

---

## Fase 2 — Migrazione .NET 8 → .NET 10 + EF Core 10 built-in vector

**Quando:** entro ottobre 2026 (buffer di un mese prima dell'EOL del 10 novembre)  
**Rischio:** medio — breaking change ASP.NET Core noti e gestibili  
**Prerequisito SQL Server:** SQL Server **2025** o **Azure SQL** (LocalDB 2022 NON supporta EF Core 10 vector built-in)

### Vincolo SQL Server locale ⚠️

EF Core 10 built-in vector support richiede SQL Server 2025 o Azure SQL.
LocalDB 2022 (usato attualmente per sviluppo locale) **non è supportato**.

Opzioni per lo sviluppo locale in Fase 2:

| Opzione | Pro | Contro |
|---------|-----|--------|
| Docker con SQL Server 2025 | Gratuito, isolato | Richiede Docker Desktop |
| Azure SQL (tunnel) | Uguale all'ambiente demo | Richiede connessione, costi |
| Mock del vector search nei test | Semplice | Non testa il layer SQL |

**Raccomandato:** Docker con SQL Server 2025.

```bash
docker pull mcr.microsoft.com/mssql/server:2025-latest
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name sqlserver2025 \
  mcr.microsoft.com/mssql/server:2025-latest
```

Aggiorna la connection string in User Secrets per lo sviluppo locale:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=Sharp4AiDemo;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

### 2.1 — Aggiorna il `.csproj`

```xml
<!-- Target framework -->
<TargetFramework>net10.0</TargetFramework>

<!-- EF Core 10: rimuovere EFCore.SqlServer.VectorSearch, built-in -->
<!-- RIMUOVERE: <PackageReference Include="EFCore.SqlServer.VectorSearch" ... /> -->

<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />

<!-- Aggiorna gli altri package alle versioni .NET 10 compatibili -->
<PackageReference Include="Microsoft.SemanticKernel" Version="*" />
<PackageReference Include="Serilog.AspNetCore" Version="*" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="*" />
<PackageReference Include="Hangfire" Version="*" />
<PackageReference Include="Hangfire.AspNetCore" Version="*" />
<PackageReference Include="Hangfire.InMemory" Version="*" />
```

> Usare le versioni `*` per ottenere l'ultima stabile con `dotnet restore`, poi fissarle nel `.csproj`.

### 2.2 — Breaking change ASP.NET Core 10 da verificare

| Breaking change | File da verificare | Azione |
|----------------|-------------------|--------|
| `WithOpenApi()` deprecato | `Program.cs` / Swagger setup | Swashbuckle 7.x usa metodi propri, non `WithOpenApi()` — ok se non usato direttamente |
| `WebHostBuilder` / `IWebHost` obsoleti | `Program.cs` | Verifica si usa `Host.CreateDefaultBuilder()` (corretto) o `WebHost.CreateDefaultBuilder()` (da aggiornare) |
| `IActionContextAccessor` obsoleto | Controller | Cerca `IActionContextAccessor` nel progetto e sostituisci |
| Cookie auth redirect per API endpoints | `Program.cs` | Comportamento cambiato; verifica se usato |

```bash
# Cerca i pattern da aggiornare
grep -r "WebHost.CreateDefaultBuilder\|IActionContextAccessor\|WithOpenApi" Sharp4AI.Demo.Api/ --include="*.cs"
```

### 2.3 — EF Core 10: vettori built-in

Con EF Core 10 i vettori sono nativi. Se la Fase 1 è già stata fatta (tipo `SqlVector<float>`),
il codice applicativo **non cambia**. Basta rimuovere il package e aggiungere una migration di verifica:

```bash
dotnet ef migrations add EfCore10BuiltinVector
dotnet ef database update
```

EF Core 10 aggiunge anche `VECTOR_SEARCH()` per approximate nearest neighbor (ANN) con indice vettoriale:

```csharp
// Ricerca approssimata (più veloce su grandi dataset, richiede vector index)
var results = await context.DocumentChunks
    .VectorSearch(d => d.Embedding, queryVector, "cosine")
    .OrderBy(r => r.Distance)
    .Take(topN)
    .WithApproximate()
    .ToListAsync();
```

Per abilitare l'indice vettoriale nel modello:

```csharp
// In DbContext.OnModelCreating
modelBuilder.Entity<DocumentChunk>()
    .HasVectorIndex(d => d.Embedding, "cosine");
```

### 2.4 — Swashbuckle → valuta Microsoft.AspNetCore.OpenApi

.NET 10 include OpenAPI built-in. Swashbuckle 7.x continua a funzionare.
La valutazione della migrazione a OpenAPI built-in è **opzionale** per questa fase.

### 2.5 — Build, fix warning, test

```bash
dotnet build
# Risolvi tutti i warning come errori di obsolescenza
dotnet run
# Verifica POST /api/similarity
# Verifica Hangfire dashboard /jobs
# Verifica health check /health
```

### 2.6 — Aggiorna README e SETUP

Aggiorna i prerequisiti in `README.md` e `SETUP.md`:
- Da `.NET 8 SDK` a `.NET 10 SDK`
- Da `SQL Server 2022+` a `SQL Server 2025+ o Azure SQL` (per vector)
- Aggiungi le istruzioni Docker per SQL Server 2025

### 2.7 — Applica al secondo repo

Copia le modifiche in `D:\Sessioni\Sharp4Ai Pescara\Sharp4Ai-CopilotStudio\Demo`.

---

## Checklist complessiva

### Fase 1 (ora)
- [ ] Aggiorna EF Core da 8.0.11 a 9.0.8 nel `.csproj`
- [ ] Aggiorna EFCore.SqlServer.VectorSearch da 0.2.0 a 9.0.0
- [ ] Adatta le entity e query vettoriali a `SqlVector<float>`
- [ ] Crea migration `UpgradeToNativeVector` e applica al DB locale
- [ ] Build verde + test `/api/similarity`
- [ ] Commit repo privato
- [ ] Applica modifiche identiche al repo pubblico + commit

### Fase 2 (entro ottobre 2026)
- [ ] Predisponi ambiente locale con SQL Server 2025 (Docker)
- [ ] Aggiorna `TargetFramework` a `net10.0`
- [ ] Rimuovi `EFCore.SqlServer.VectorSearch`, aggiorna EF Core a 10.x
- [ ] Aggiorna tutti i package a versioni .NET 10 compatibili
- [ ] Risolvi breaking change ASP.NET Core 10 (`grep` sui pattern obsoleti)
- [ ] Crea migration `EfCore10BuiltinVector` e applica
- [ ] Valuta `HasVectorIndex` + `VectorSearch()` per performance ANN
- [ ] Build verde + test funzionale completo
- [ ] Aggiorna `README.md` e `SETUP.md`
- [ ] Commit repo privato
- [ ] Applica modifiche identiche al repo pubblico + commit

---

## Riferimenti

| Risorsa | Link |
|---------|------|
| Breaking changes .NET 10 | https://learn.microsoft.com/dotnet/core/compatibility/10 |
| Breaking changes ASP.NET Core 10 | https://learn.microsoft.com/aspnet/core/breaking-changes/10/overview |
| EF Core 10 — What's New | https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew |
| EF Core 10 — Vector search SQL Server | https://learn.microsoft.com/ef/core/providers/sql-server/vector-search |
| Build 2026 — TT656 .NET EOL migration | https://build.microsoft.com/sessions/TT656 |
| Build 2026 — OD802 .NET 11 agentic web | https://medius.microsoft.com/Embed/video-nc/193ed653-1a1c-4bb1-b190-d5cdf880fb9a |
| Build 2026 — OD805 AI Building Blocks .NET | https://medius.microsoft.com/Embed/video-nc/77f11a3c-9f3d-4ac6-9ea4-fef86c97f73a |
