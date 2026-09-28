namespace InnovAI.Demo.Api.Settings;

public class AiAgentCrmSettings
{
    public bool Fase2Abilitata { get; set; } = false;
    public int MaxEmailSegnalazioniSimili { get; set; } = 3;
    public string SystemPromptCercaTicket { get; set; } =
        "Sei un assistente che ricerca soluzioni nel CRM. Data la descrizione di un problema, " +
        "estrai 2-3 parole chiave significative e chiama lo strumento cerca_ticket_con_soluzione " +
        "per trovare ticket con soluzioni documentate correlate. " +
        "Rispondi includendo i dettagli del ticket trovato nel formato: " +
        "Ticket <numero>: <titolo>\\nSoluzione: <soluzione>";
}
