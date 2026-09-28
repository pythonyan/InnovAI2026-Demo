using InnovAI.Demo.Api.DTO;
using InnovAI.Demo.Api.Interfaces;

namespace InnovAI.Demo.Api.Stubs;

/// <summary>
/// Stub CRM con 3 ticket hardcoded per la demo.
/// Sostituire con CrmProxyService reale e credenziali CRM per ambienti non-demo.
/// </summary>
public class DemoCrmProxyService : ICrmProxyService
{
    private static readonly List<CrmTicket> DemoTickets =
    [
        new CrmTicket
        {
            Ticket_No = "TT101876",
            Ticket_Title = "Multa non visibile dopo notifica raccomandata",
            Solution = "Verificare il codice fiscale in anagrafica: se la multa è stata emessa con CF errato non compare nell'area personale. Procedura: backoffice → Gestione avvisi → cerca per targa → modifica CF beneficiario.",
            TicketStatus = "Closed",
            Email_From = "cittadino@example.com"
        },
        new CrmTicket
        {
            Ticket_No = "TT099443",
            Ticket_Title = "Impossibile autenticarsi con SPID livello 2",
            Solution = "Il problema si verifica quando l'Identity Provider SPID restituisce un attributo fiscalNumber maiuscolo/minuscolo diverso da quello registrato. Soluzione: normalizzare il CF in uppercase nel profilo utente prima del confronto.",
            TicketStatus = "Closed",
            Email_From = "cittadino2@example.com"
        },
        new CrmTicket
        {
            Ticket_No = "TT102204",
            Ticket_Title = "Pagamento PagoPA non confermato dopo redirect",
            Solution = "Il redirect di ritorno dal PSP richiede HTTPS su entrambi i lati. Verificare che l'URL_RETURN in ExternalServiceConfiguration non sia HTTP. Se l'ambiente è dev con dev-tunnel, aggiornare il callback URL al tunnel attivo.",
            TicketStatus = "Closed",
            Email_From = "cittadino3@example.com"
        }
    ];

    public Task<CrmDataResponse?> GetCrmDataAsync(string emailCittadino)
    {
        return Task.FromResult<CrmDataResponse?>(new CrmDataResponse
        {
            Status = 200,
            Data = DemoTickets
        });
    }

    public Task<CrmDataResponse?> GetCrmTicketsByKeywordsAsync(string keywords)
    {
        var lower = keywords.ToLowerInvariant();
        var filtered = DemoTickets
            .Where(t =>
                (t.Ticket_Title?.ToLowerInvariant().Contains(lower) ?? false) ||
                (t.Solution?.ToLowerInvariant().Contains(lower) ?? false))
            .ToList();

        if (filtered.Count == 0)
            filtered = DemoTickets.Take(2).ToList();

        return Task.FromResult<CrmDataResponse?>(new CrmDataResponse
        {
            Status = 200,
            Data = filtered
        });
    }
}
