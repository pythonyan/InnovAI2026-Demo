namespace InnovAI.Demo.Api.Settings;

public class CrmApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Parametro query per la ricerca per email (default: "email").
    /// </summary>
    public string EmailQueryParam { get; set; } = "email";

    /// <summary>
    /// Parametro query per la ricerca per keywords (default: "keywords").
    /// </summary>
    public string KeywordsQueryParam { get; set; } = "keywords";

    /// <summary>
    /// Timeout in secondi per le chiamate HTTP al CRM (default: 10).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
