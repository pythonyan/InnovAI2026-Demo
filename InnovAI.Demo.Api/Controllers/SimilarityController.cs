using Microsoft.AspNetCore.Mvc;
using InnovAI.Demo.Api.Filters;
using InnovAI.Demo.Api.Interfaces;
using InnovAI.Demo.Api.Services;
using InnovAI.Demo.Api.ViewModels;
using System.Net;
using System.Text.Json;

namespace InnovAI.Demo.Api.Controllers;

/// <summary>
/// Endpoint dedicato per Copilot Studio: ricerca segnalazioni simili tramite embedding
/// e arricchimento CRM (AI Agent SK Fase 2).
/// </summary>
[ApiController]
[Route("api/similarity")]
[ApiKeyAuth]
public class SimilarityController(
    VectorSearchService vectorSearchService,
    IAiAgentCrmService aiAgentCrmService,
    ILogger<SimilarityController> logger) : ControllerBase
{
    /// <summary>
    /// Cerca le segnalazioni più simili al testo fornito e arricchisce con ticket CRM.
    /// Progettato per essere chiamato direttamente da Copilot Studio HTTP Action.
    /// </summary>
    /// <remarks>
    /// Richiede header: Authorization: Bearer {ApiKey}  oppure  X-Api-Key: {ApiKey}
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(SimilarityResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<SimilarityResponse>> FindSimilar(
        [FromBody] SimilarityRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        logger.LogInformation("SimilarityController: ricerca per testo '{Testo}' topN={TopN}",
            request.Testo[..Math.Min(50, request.Testo.Length)], request.TopN);

        // Passo 1: ricerca vettoriale nel DB embedding
        var simResults = await vectorSearchService.FindSimilarTexts(request.Testo, request.TopN, cancellationToken);

        if (simResults.Count == 0)
        {
            logger.LogInformation("SimilarityController: nessun risultato trovato nel DB embedding");
            return Ok(new SimilarityResponse());
        }

        // Passo 2: arricchimento CRM tramite AI Agent (Fase 2)
        var filtrati = simResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Descrizione))
            .ToList();

        var crmResult = await aiAgentCrmService.TrovaSegnalazioniSimiliCrmAsync(
            filtrati, "demo", cancellationToken);

        // Mappa la risposta per Copilot Studio
        var tickets = crmResult.Segnalazioni
            .Select(s => new SimilarityTicket
            {
                TicketId = s.TicketCrmId ?? "N/D",
                Titolo = CleanDescription(s.Descrizione),
                Soluzione = s.SoluzioneCrm,
                Similarity = simResults
                    .FirstOrDefault(r => r.Descrizione == s.Descrizione)?.Similarity ?? 0
            })
            .OrderByDescending(t => t.Similarity)
            .ToList();

        logger.LogInformation("SimilarityController: restituiti {Count} ticket", tickets.Count);

        return Ok(new SimilarityResponse { Tickets = tickets });
    }
    private static string CleanDescription(string descrizione)
    {
        if (string.IsNullOrWhiteSpace(descrizione)) return "N/D";

        // Se è JSON serializzato, estrai la Descrizione interna
        if (descrizione.TrimStart().StartsWith("{"))
        {
            try
            {
                var obj = JsonSerializer.Deserialize<JsonElement>(descrizione);
                if (obj.TryGetProperty("Descrizione", out var desc))
                    return desc.GetString()?[..Math.Min(100, desc.GetString()!.Length)] ?? "N/D";
                if (obj.TryGetProperty("TipologiaSegnalazione", out var tipo))
                    return tipo.GetString()?[..Math.Min(100, tipo.GetString()!.Length)] ?? "N/D";
            }
            catch { }
        }

        return descrizione[..Math.Min(100, descrizione.Length)];
    }
}
