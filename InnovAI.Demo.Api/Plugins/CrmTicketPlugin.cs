using System.ComponentModel;
using System.Text;
using InnovAI.Demo.Api.Interfaces;

namespace InnovAI.Demo.Api.Plugins;

/// <summary>
/// Tool CRM esposti all'agente AI (Microsoft Agent Framework) tramite AIFunctionFactory.Create.
/// Le funzioni sono invocabili tramite tool calling e restituiscono testo formattato
/// con numero ticket e soluzione, pronto per essere consumato dall'LLM.
/// </summary>
public sealed class CrmTicketPlugin(ICrmProxyService crmProxyService, ILogger<CrmTicketPlugin> logger)
{
    /// <summary>
    /// Recupera tutti i ticket CRM risolti (con soluzione documentata) associati all'email dell'utente
    /// e li restituisce come testo formattato "Ticket XXXXX: titolo / Soluzione: ...".
    /// Restituisce stringa vuota se non esistono ticket con soluzione o in caso di errore.
    /// </summary>
    /// <param name="emailUtente">Email dell'utente di cui recuperare i ticket CRM risolti.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>Testo con l'elenco dei ticket risolti, oppure stringa vuota.</returns>
    [Description("Recupera le soluzioni dei ticket CRM già risolti per un utente, identificato dalla sua email")]
    public async Task<string> RecuperaSoluzioniCrmAsync(
        [Description("Email dell'utente di cui recuperare i ticket CRM risolti")] string emailUtente,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("CrmTicketPlugin: recupero soluzioni CRM per {Email}", emailUtente);
        try
        {
            var crmData = await crmProxyService.GetCrmDataAsync(emailUtente);
            if (crmData == null || crmData.Status != 200 || crmData.Data == null)
                return string.Empty;

            var ticketConSoluzione = crmData.Data.Where(t => !string.IsNullOrWhiteSpace(t.Solution)).ToList();
            if (ticketConSoluzione.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (var ticket in ticketConSoluzione)
            {
                sb.AppendLine($"Ticket {ticket.Ticket_No}: {ticket.Ticket_Title}");
                sb.AppendLine($"Soluzione: {ticket.Solution}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CrmTicketPlugin: errore recupero soluzioni CRM per {Email}", emailUtente);
            return string.Empty;
        }
    }

    /// <summary>
    /// Cerca nel CRM i ticket con soluzione documentata usando parole chiave estratte dalla descrizione del problema.
    /// Restituisce al massimo i primi 3 risultati come testo formattato "Ticket XXXXX: titolo / Soluzione: ...".
    /// Restituisce stringa vuota se nessun ticket corrisponde o in caso di errore.
    /// </summary>
    /// <param name="keywords">2-3 parole chiave significative estratte dalla descrizione del problema.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns>Testo con i ticket trovati (max 3), oppure stringa vuota.</returns>
    [Description("Cerca ticket CRM che abbiano una soluzione documentata, usando parole chiave estratte dalla descrizione del problema")]
    public async Task<string> CercaTicketConSoluzioneAsync(
        [Description("2-3 parole chiave significative estratte dalla descrizione del problema")] string keywords,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("CrmTicketPlugin: ricerca ticket per keywords '{Keywords}'", keywords);
        try
        {
            var crmData = await crmProxyService.GetCrmTicketsByKeywordsAsync(keywords);
            if (crmData == null || crmData.Status != 200 || crmData.Data == null || crmData.Data.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var ticket in crmData.Data.Take(3))
            {
                sb.AppendLine($"Ticket {ticket.Ticket_No}: {ticket.Ticket_Title}");
                sb.AppendLine($"Soluzione: {ticket.Solution}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CrmTicketPlugin: errore ricerca ticket per keywords '{Keywords}'", keywords);
            return string.Empty;
        }
    }
}
