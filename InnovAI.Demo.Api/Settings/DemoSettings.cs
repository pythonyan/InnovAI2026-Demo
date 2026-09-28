namespace InnovAI.Demo.Api.Settings;

/// <summary>
/// Configurazione fissa  per la demo (sostituisce IBusinessConfigurationService).
/// Popola in appsettings.json → sezione "Demo".
/// </summary>
public class DemoSettings
{
    public string Codice { get; set; } = "codice_demo";
    public string Nome { get; set; } = "Nome Demo";
    public string EmailAssistenza { get; set; } = "assistenza@demo.it";
    public string EmailDiContattoHelpDesk { get; set; } = "helpdesk@demo.it";
    public List<string> ListaTipologiaErrore { get; set; } =
    [
        "Multa / Sanzione",
        "SPID / Autenticazione",
        "PagoPA / Pagamento",
        "Avviso non trovato",
        "Altro"
    ];
    public string ApiKey { get; set; } = "CHANGE-ME-BEFORE-DEMO";
}
