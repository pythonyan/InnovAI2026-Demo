using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using InnovAI.Demo.Api.Interfaces;
using InnovAI.Demo.Api.Plugins;
using InnovAI.Demo.Api.DTO;
using InnovAI.Demo.Api.Settings;

namespace InnovAI.Demo.Api.Services;

public class AiAgentCrmService(
    IChatClient chatClient,
    ICrmProxyService crmProxyService,
    IOptions<AiAgentCrmSettings> settings,
    ILoggerFactory loggerFactory) : IAiAgentCrmService
{
    private readonly AiAgentCrmSettings _settings = settings.Value;
    private readonly ILogger<AiAgentCrmService> _logger = loggerFactory.CreateLogger<AiAgentCrmService>();

    /// <summary>
    /// Cerca nel CRM i ticket correlati alle segnalazioni simili trovate dalla ricerca vettoriale.
    /// Usa CodiceSegnalazione e Soluzione già noti dal vettoriale come fonte primaria;
    /// il CRM agent viene consultato solo se uno dei due campi manca.
    /// </summary>
    public async Task<RisultatoSegnalazioniSimili> TrovaSegnalazioniSimiliCrmAsync(
        IReadOnlyList<VectorSearchService.SimilarTextResult> segnalazioniSimili,
        string codiceEnte,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Fase2Abilitata)
        {
            _logger.LogDebug("AiAgentCrmService: Fase 2 non abilitata — skip");
            return new RisultatoSegnalazioniSimili(false, []);
        }

        if (segnalazioniSimili.Count == 0)
        {
            _logger.LogDebug("AiAgentCrmService: nessuna segnalazione simile — skip");
            return new RisultatoSegnalazioniSimili(false, []);
        }

        try
        {
            var risultato = await RicercaTicketCrmConAgenteAsync(segnalazioniSimili, cancellationToken);
            _logger.LogInformation(
                "AiAgentCrmService: {Count} segnalazioni arricchite con CRM per {CodiceEnte}",
                risultato.Count, codiceEnte);
            return new RisultatoSegnalazioniSimili(risultato.Count > 0, risultato);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiAgentCrmService: errore ricerca CRM per {CodiceEnte} — fallback graceful", codiceEnte);
            return new RisultatoSegnalazioniSimili(false, []);
        }
    }

    /// <summary>
    /// Popola le segnalazioni simili usando prima i dati dal vettoriale (CodiceSegnalazione, Soluzione);
    /// chiama il CRM agent solo se uno dei due campi manca.
    /// Al termine deduplicà i risultati raggruppando per soluzione CRM.
    /// </summary>
    private async Task<List<SegnalazioneSimileInfo>> RicercaTicketCrmConAgenteAsync(
        IReadOnlyList<VectorSearchService.SimilarTextResult> segnalazioni,
        CancellationToken cancellationToken)
    {
        var crmPlugin = new CrmTicketPlugin(crmProxyService, loggerFactory.CreateLogger<CrmTicketPlugin>());

        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "CrmTicketAgent",
            ChatOptions = new ChatOptions
            {
                Instructions = _settings.SystemPromptCercaTicket,
                MaxOutputTokens = 500,
                Tools =
                [
                    AIFunctionFactory.Create(crmPlugin.RecuperaSoluzioniCrmAsync, name: "recupera_soluzioni_crm"),
                    AIFunctionFactory.Create(crmPlugin.CercaTicketConSoluzioneAsync, name: "cerca_ticket_con_soluzione")
                ]
            }
        }, loggerFactory, null!);

        var risultato = new List<SegnalazioneSimileInfo>();

        foreach (var segnalazione in segnalazioni.Take(_settings.MaxEmailSegnalazioniSimili))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Dati già noti dalla ricerca vettoriale: usati come fonte primaria
            string? ticketId = segnalazione.CodiceSegnalazione;
            string? soluzione = segnalazione.Soluzione;

            // Arricchimento opzionale da CRM solo se mancano i dati dalla ricerca vettoriale
            if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(soluzione))
            {
                try
                {
                    var session = await agent.CreateSessionAsync(cancellationToken);
                    var risposta = await agent.RunAsync(
                        $"Descrizione del problema: {segnalazione.Descrizione}",
                        session,
                        null,
                        cancellationToken);

                    var (crmTicketId, crmSoluzione) = EstraiTicketESoluzione(risposta.ToString() ?? string.Empty);
                    ticketId ??= crmTicketId;
                    soluzione ??= crmSoluzione;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AiAgentCrmService: errore per una descrizione — includo senza ticket CRM");
                }
            }

            risultato.Add(new SegnalazioneSimileInfo(segnalazione.Descrizione ?? string.Empty, ticketId, soluzione));
        }

        // Deduplicazione per ticket: stesso ticket non deve comparire due volte;
        // se manca il ticket id, si usa la soluzione come chiave secondaria.
        return risultato
            .GroupBy(s => string.IsNullOrWhiteSpace(s.TicketCrmId)
                ? (string.IsNullOrWhiteSpace(s.SoluzioneCrm)
                    ? s.Descrizione.Trim().ToLowerInvariant()
                    : s.SoluzioneCrm.Trim().ToLowerInvariant())
                : s.TicketCrmId.Trim().ToLowerInvariant())
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Analizza il testo libero restituito dall'agente AI ed estrae il numero di ticket CRM e la soluzione.
    /// Si aspetta righe nel formato "Ticket XXXXX: ..." e "Soluzione: ...".
    /// </summary>
    private static (string? ticketId, string? soluzione) EstraiTicketESoluzione(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return (null, null);

        string? ticketId = null;
        string? soluzione = null;

        foreach (var riga in testo.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var r = riga.Trim();
            if (ticketId == null && r.StartsWith("Ticket ", StringComparison.OrdinalIgnoreCase))
            {
                var idx = r.IndexOf(':');
                if (idx > 7) ticketId = r[7..idx].Trim();
            }
            if (soluzione == null && r.StartsWith("Soluzione:", StringComparison.OrdinalIgnoreCase))
            {
                var s = r[10..].Trim();
                if (!string.IsNullOrWhiteSpace(s)) soluzione = s;
            }
        }

        return (ticketId, soluzione);
    }
}
