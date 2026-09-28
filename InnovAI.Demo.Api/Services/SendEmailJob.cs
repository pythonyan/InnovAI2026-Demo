using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Hangfire;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using InnovAI.Demo.Api.Data;
using InnovAI.Demo.Api.Data.Entities;
using InnovAI.Demo.Api.DTO;
using InnovAI.Demo.Api.Interfaces;
using InnovAI.Demo.Api.Settings;

namespace InnovAI.Demo.Api.Services;

/// <summary>
/// Job Hangfire responsabile dell'invio email di segnalazione e del salvataggio dei dati correlati.
/// Esegue l'intero flusso end-to-end: analisi sentiment, ricerca vettoriale segnalazioni simili (Fase 1),
/// arricchimento CRM via agente AI (Fase 2), composizione del messaggio MIME da template,
/// invio SMTP, persistenza su DB e indicizzazione vettoriale per ricerche future.
/// In caso di errore rilancia l'eccezione per abilitare i 3 retry automatici di Hangfire.
/// </summary>
[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
public class SendEmailJob(
    IConfiguration configuration,
    ILogger<SendEmailJob> logger,
    IBusinessConfigurationService entiConfigurationService,
    DemoDbContext context,
    ITextAnalysisService textAnalysisService,
    TemplateProcessor templateProcessor,
    VectorSearchService vectorSearchService,
    IAiAgentCrmService aiAgentCrmService,
    IOptions<AiAgentCrmSettings> aiAgentCrmSettings) : ISendEmailService
{
    private readonly int _maxSegnalazioniSimili = aiAgentCrmSettings.Value.MaxEmailSegnalazioniSimili;
    /// <summary>
    /// Esegue il flusso completo di invio email per una segnalazione:
    /// recupera la configurazione ente, analizza il sentiment, cerca segnalazioni simili (vettoriale + CRM),
    /// compone il messaggio da template HTML/testo, lo invia via SMTP,
    /// persiste i dati su DB e indicizza il contenuto per ricerche vettoriali future.
    /// </summary>
    /// <param name="jobId">Identificatore univoco del job Hangfire, usato per deduplicazione e logging.</param>
    /// <param name="cognome">Cognome del cittadino che ha aperto la segnalazione.</param>
    /// <param name="email">Email del cittadino (mittente Reply-To e destinatario CRM).</param>
    /// <param name="nome">Nome del cittadino (opzionale).</param>
    /// <param name="codiceEnte">Codice IPA dell'ente. Obbligatorio: se null lancia <see cref="ArgumentNullException"/>.</param>
    /// <param name="nomeEnte">Nome leggibile dell'ente, usato nel From dell'email e nell'oggetto.</param>
    /// <param name="descrizioneProblema">Testo libero del problema segnalato, usato per sentiment e ricerca vettoriale.</param>
    /// <param name="tipologiaSegnalazione">Categoria/tipo della segnalazione.</param>
    /// <param name="telefono">Numero di telefono del cittadino (opzionale).</param>
    /// <param name="codiceFiscalePartitaIva">Codice fiscale o P.IVA del cittadino.</param>
    /// <returns><c>true</c> se l'email è stata inviata e salvata correttamente.</returns>
    public async Task<bool> SendEmailAsync(
        string jobId,
        string cognome,
        string? email,
        string? nome,
        string? codiceEnte,
        string? nomeEnte,
        string? descrizioneProblema,
        string tipologiaSegnalazione,
        string? telefono,
        string codiceFiscalePartitaIva)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = jobId,
            ["CorrelationId"] = Guid.NewGuid().ToString("N")[..8],
            ["CodiceIpa"] = codiceEnte ?? string.Empty
        });

        try
        {
            if (string.IsNullOrEmpty(codiceEnte))
                throw new ArgumentNullException(nameof(codiceEnte));

            logger.LogInformation("SendEmailAsync START — ente:{Ente} cf:{CF}", nomeEnte, codiceFiscalePartitaIva);

            var enteConfig = await entiConfigurationService.GetBusinessServiceConfiguration(codiceEnte, string.Empty)
                ?? throw new InvalidOperationException("Configurazione ente non trovata");

            var sentiment = await textAnalysisService.AnalyzeSentimentAsync([descrizioneProblema ?? string.Empty]);

            // Fase 1: ricerca vettoriale segnalazioni simili
            var segnalazioniSimili = await TrovaSegnalazioniSimiliAsync(descrizioneProblema, CancellationToken.None);

            // Fase 2: arricchimento CRM con agente SK
            var risultatoSimili = await aiAgentCrmService.TrovaSegnalazioniSimiliCrmAsync(
                segnalazioniSimili, codiceEnte, CancellationToken.None);


            #region Invio e-mail
            var username = configuration["EmailUsername"]
        ?? throw new InvalidOperationException("EmailUsername mancante in configurazione");
            var password = configuration["EmailPassword"];

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(nomeEnte, username));
            message.ReplyTo.Add(new MailboxAddress(enteConfig.EmailAssistenza, enteConfig.EmailAssistenza));
            message.To.Add(new MailboxAddress("Assistenza portale dei pagamenti", enteConfig.EmailDiContattoHelpDesk));
            message.Subject = $"[NPdC]-[{nomeEnte}]-Segnalazione sul portale del cittadino";

            var templatePath = GetTemplatePath("template_assistenza.txt");
            var (htmlContent, plainText) = templateProcessor.SubstitutePlaceholders(
                templatePath, cognome, email, nome, descrizioneProblema, telefono, tipologiaSegnalazione, sentiment);

            var (sezioneHtml, sezioneText) = CostruisciSezioneSegnalazioniSimili(risultatoSimili);
            htmlContent = htmlContent.Replace("{SegnalazioniSimili}", sezioneHtml);
            plainText = plainText.Replace("{SegnalazioniSimili}", sezioneText);

            var bodyBuilder = new BodyBuilder { TextBody = plainText, HtmlBody = htmlContent };
            message.Body = bodyBuilder.ToMessageBody();
            var smtpServer = configuration["EmailSmtpServer"];
            var smtpPort = configuration["EmailSmtpServerPort"]
                ?? throw new InvalidOperationException("EmailSmtpServerPort mancante");

            logger.LogInformation("SMTP connect {Server}:{Port}", smtpServer, smtpPort);
            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = SmtpCertCallback;

            await client.ConnectAsync(smtpServer, int.Parse(smtpPort), SecureSocketOptions.Auto);
            // Autentica solo se il server SMTP lo richiede e abbiamo una password.
            // Consente di usare server dev locali (smtp4dev, Papercut) senza auth.
            if (!string.IsNullOrWhiteSpace(password) && client.AuthenticationMechanisms.Count > 0)
                await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("SMTP send OK");
            #endregion

            #region Salvataggio ed embedding
            var mailData = new MailData
            {
                JobId = jobId,
                Cognome = cognome,
                Nome = nome ?? string.Empty,
                Telefono = telefono,
                Mail = email,
                TipologiaSegnalazione = tipologiaSegnalazione,
                DescrizioneProblema = descrizioneProblema,
                CodiceEnte = codiceEnte,
                NomeEnte = nomeEnte ?? string.Empty,
                CodiceFiscalePartitaIva = codiceFiscalePartitaIva,
                Oggetto = message.Subject,
                LastModified = DateTime.UtcNow,
                SentimentJson = JsonSerializer.Serialize(sentiment),
                CorpoHtml = htmlContent
            };
            var mailGuid = await SaveEmailDataAsync(mailData);

            var embeddingContent = JsonSerializer.Serialize(new
            {
                mailData.TipologiaSegnalazione,
                mailData.DescrizioneProblema
            });
            using (var embeddingStream = new MemoryStream(Encoding.UTF8.GetBytes(embeddingContent)))
            {
                await vectorSearchService.ImportAsync(embeddingStream, $"Email-{mailGuid}", "application/json", mailGuid, CancellationToken.None);
            }
            // log token spesi
            logger.LogInformation("SendEmailAsync END — mailGuid:{MailGuid}", mailGuid);
            return true; 
            #endregion
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SendEmailAsync ERROR");
            throw; // rilancia per far marcare il job come Failed in Hangfire e abilitare i retry
        }
    }

    /// <summary>
    /// Invia l'email di segnalazione senza passare per Hangfire, generando un jobId casuale.
    /// Utile per invii sincroni o in contesti dove il job scheduler non è disponibile.
    /// </summary>
    /// <returns><c>true</c> se l'email è stata inviata correttamente.</returns>
    public async Task<bool> SendEmailWithoutJobAsync(
        string cognome, string? email, string? nome, string? codiceIpa,
        string? nomeEnte, string? descrizioneProblema, string tipologiaSegnalazione,
        string? telefono, string codiceFiscalePartitaIva)
        => await SendEmailAsync(Guid.NewGuid().ToString(), cognome, email, nome, codiceIpa, nomeEnte,
            descrizioneProblema, tipologiaSegnalazione, telefono, codiceFiscalePartitaIva);

    /// <summary>
    /// Restituisce l'elenco delle tipologie di segnalazione configurate per l'ente identificato dal codice IPA.
    /// Lancia <see cref="InvalidOperationException"/> se la configurazione non è trovata.
    /// </summary>
    /// <param name="codiceIpa">Codice IPA dell'ente.</param>
    /// <returns>Lista di stringhe con le tipologie di errore/segnalazione configurate.</returns>
    public async Task<List<string>> GetTipologiaErroreAsync(string codiceIpa)
    {
        var config = await entiConfigurationService.GetBusinessServiceConfiguration(codiceIpa, string.Empty)
            ?? throw new InvalidOperationException("Configurazione ente non trovata");
        return config.ListaTipologiaErrore ?? [];
    }

    /// <summary>
    /// Persiste i dati della segnalazione email nel database e restituisce il GUID assegnato.
    /// In caso di errore registra il log e rilancia l'eccezione.
    /// </summary>
    /// <param name="mailData">Entità contenente tutti i dati della segnalazione da salvare.</param>
    /// <returns>Il <see cref="Guid"/> dell'entità salvata.</returns>
    public async Task<Guid> SaveEmailDataAsync(MailData mailData)
    {
        try
        {
            await context.MailData.AddAsync(mailData);
            await context.SaveChangesAsync();
            return mailData.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveEmailDataAsync ERROR");
            throw;
        }
    }

    /// <summary>
    /// Recupera dal database le segnalazioni associate al codice fiscale/P.IVA specificato,
    /// con filtro opzionale per codice IPA ente. Il confronto è case-insensitive.
    /// </summary>
    /// <param name="codiceFiscale">Codice fiscale o partita IVA del cittadino.</param>
    /// <param name="codiceEnte">Codice IPA ente per filtrare i risultati (opzionale).</param>
    /// <returns>Lista di <see cref="MailData"/> corrispondenti ai criteri.</returns>
    public async Task<List<MailData>> RetrieveSegnalazioniAsync(string codiceFiscale, string? codiceEnte)
    {
        var q = context.MailData.Where(p => p.CodiceFiscalePartitaIva.ToUpper() == codiceFiscale.ToUpper());
        if (!string.IsNullOrEmpty(codiceEnte))
        {
            var ipaNorm = codiceEnte.ToUpper();
            q = q.Where(p => p.CodiceEnte != null && p.CodiceEnte.ToUpper() == ipaNorm);
        }
        return await q.ToListAsync();
    }


    /// <summary>
    /// Esegue la ricerca vettoriale (Fase 1) per trovare le segnalazioni storicamente simili
    /// a quella corrente, con descrizione, codice ticket e soluzione già estratti dal JSON del chunk.
    /// Restituisce al massimo 3 risultati distinti e non vuoti.
    /// In caso di errore esegue un fallback graceful restituendo una lista vuota.
    /// </summary>
    private async Task<IReadOnlyList<VectorSearchService.SimilarTextResult>> TrovaSegnalazioniSimiliAsync(
        string? descrizioneProblema,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(descrizioneProblema))
            return [];

        try
        {
            var risultati = await vectorSearchService.FindSimilarTexts(descrizioneProblema, maxResults: _maxSegnalazioniSimili, cancellationToken);
            if (risultati.Count == 0) return [];

            var filtrati = risultati
                .Where(r => !string.IsNullOrWhiteSpace(r.Descrizione))
                .DistinctBy(r => r.CodiceSegnalazione ?? r.Descrizione)
                .ToList();

            logger.LogInformation("TrovaSegnalazioniSimili: {Count} simili", filtrati.Count);
            return filtrati;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TrovaSegnalazioniSimili: errore — fallback graceful");
            return [];
        }
    }

    /// <summary>
    /// Genera la sezione HTML e testo-piatto delle segnalazioni simili da iniettare nel template email
    /// al placeholder <c>{SegnalazioniSimili}</c>. Restituisce tuple di stringhe vuote se non ci sono simili.
    /// I valori HTML sono codificati per prevenire injection.
    /// </summary>
    /// <param name="risultato">Risultato della Fase 2 con le segnalazioni arricchite da ticket e soluzione CRM.</param>
    /// <returns>Tupla <c>(html, text)</c> con il frammento HTML e la versione plain-text della sezione.</returns>
    private static (string html, string text) CostruisciSezioneSegnalazioniSimili(RisultatoSegnalazioniSimili risultato)
    {
        if (!risultato.TrovateSimili || risultato.Segnalazioni.Count == 0)
            return (string.Empty, string.Empty);

        var sbHtml = new StringBuilder();
        sbHtml.AppendLine("<div style='background-color:#e3f2fd;padding:15px;border-left:4px solid #2196F3;margin:15px 0;border-radius:4px;'>");
        sbHtml.AppendLine("<h3 style='color:#1565c0;margin-top:0;'>&#128269; Segnalazioni simili precedenti</h3>");
        sbHtml.AppendLine("<table style='width:100%;border-collapse:collapse;'>");
        sbHtml.AppendLine("<thead><tr>" +
            "<th style='padding:8px;border:1px solid #ddd;'>Ticket CRM</th>" +
            "<th style='padding:8px;border:1px solid #ddd;'>Descrizione simile</th>" +
            "<th style='padding:8px;border:1px solid #ddd;'>Soluzione documentata</th>" +
            "</tr></thead><tbody>");

        var sbText = new StringBuilder();
        sbText.AppendLine("\nSEGNALAZIONI SIMILI:");

        foreach (var s in risultato.Segnalazioni)
        {
            var ticketHtml = string.IsNullOrWhiteSpace(s.TicketCrmId) ? "N/D" : System.Web.HttpUtility.HtmlEncode(s.TicketCrmId);
            var soluzioneHtml = string.IsNullOrWhiteSpace(s.SoluzioneCrm)
                ? "<em style='color:#999;'>—</em>"
                : System.Web.HttpUtility.HtmlEncode(s.SoluzioneCrm);

            sbHtml.AppendLine(
                $"<tr>" +
                $"<td style='padding:8px;border:1px solid #ddd;'>{ticketHtml}</td>" +
                $"<td style='padding:8px;border:1px solid #ddd;'>{System.Web.HttpUtility.HtmlEncode(s.Descrizione)}</td>" +
                $"<td style='padding:8px;border:1px solid #ddd;'>{soluzioneHtml}</td>" +
                $"</tr>");

            var ticketText = string.IsNullOrWhiteSpace(s.TicketCrmId) ? "N/D" : s.TicketCrmId;
            var soluzioneText = string.IsNullOrWhiteSpace(s.SoluzioneCrm) ? "—" : s.SoluzioneCrm;
            sbText.AppendLine($"- Ticket: {ticketText} | {s.Descrizione} | Soluzione: {soluzioneText}");
        }

        sbHtml.AppendLine("</tbody></table></div>");
        return (sbHtml.ToString(), sbText.ToString());
    }

    /// <summary>
    /// Risolve il percorso assoluto del template email nella cartella <c>Templates/</c> relativa all'assembly.
    /// Lancia <see cref="FileNotFoundException"/> se il file non esiste.
    /// </summary>
    /// <param name="templateFile">Nome del file template (es. "template_assistenza.txt").</param>
    /// <returns>Percorso assoluto del file template.</returns>
    private static string GetTemplatePath(string templateFile)
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Impossibile determinare il percorso dell'assembly");
        var path = Path.Combine(assemblyDir, "Templates", templateFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template '{templateFile}' non trovato in '{path}'.");
        return path;
    }

    /// <summary>
    /// Callback di validazione del certificato SSL per la connessione SMTP.
    /// Accetta il certificato se non ci sono errori SSL; in caso contrario registra un warning e rifiuta.
    /// </summary>
    /// <param name="sender">Oggetto mittente della callback (SmtpClient).</param>
    /// <param name="cert">Certificato del server remoto.</param>
    /// <param name="chain">Catena di certificazione.</param>
    /// <param name="errors">Errori SSL rilevati.</param>
    /// <returns><c>true</c> se il certificato è valido, <c>false</c> altrimenti.</returns>
    private bool SmtpCertCallback(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None) return true;
        logger.LogWarning("SMTP SSL errors: {Errors}", errors);
        return false;
    }
}
