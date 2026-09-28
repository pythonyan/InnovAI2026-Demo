using System.Diagnostics;
using System.Net;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InnovAI.Demo.Api.Data;
using InnovAI.Demo.Api.DTO;
using InnovAI.Demo.Api.Interfaces;
using InnovAI.Demo.Api.Services;
using InnovAI.Demo.Api.ViewModels;

namespace InnovAI.Demo.Api.Controllers;

/// <summary>
/// Controller REST per la gestione delle segnalazioni e dell'invio email tramite Hangfire.
/// Espone endpoint per accodare job di invio mail, recuperare segnalazioni esistenti,
/// ottenere i tipi di segnalazione configurati e interrogare direttamente il CRM.
/// </summary>
[ApiController]
[Route("api/mail")]
public class MailController(ILogger<MailController> logger) : ControllerBase
{
    /// <summary>
    /// Apre uno scope di logging strutturato con CorrelationId, Action e RemoteIp,
    /// avvia uno stopwatch e registra l'evento START dell'azione.
    /// </summary>
    /// <param name="action">Nome dell'azione corrente (usato nei log).</param>
    /// <returns>Tupla con lo scope di logging (da disporre nel finally) e lo stopwatch avviato.</returns>
    private (IDisposable Scope, Stopwatch Watch) BeginRequestScope(string action)
    {
        var sw = Stopwatch.StartNew();
        var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = HttpContext.TraceIdentifier,
            ["Action"] = action,
            ["RemoteIp"] = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        logger.LogInformation("START {Action}", action);
        return (scope, sw);
    }

    /// <summary>
    /// Ferma lo stopwatch e registra l'evento END dell'azione con status HTTP e tempo trascorso in ms.
    /// </summary>
    /// <param name="action">Nome dell'azione corrente.</param>
    /// <param name="sw">Stopwatch avviato da <see cref="BeginRequestScope"/>.</param>
    /// <param name="status">Codice HTTP della risposta, usato nel log.</param>
    private void LogEnd(string action, Stopwatch sw, int status)
    {
        sw.Stop();
        logger.LogInformation("END {Action} ({Status}) in {ElapsedMs}ms", action, status, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// POST api/mail/mailsenderjob — Accoda un job Hangfire di invio email per una segnalazione autenticata.
    /// Valida il modello e i campi obbligatori prima di accodare; restituisce <c>{ success: true }</c> in caso di successo.
    /// </summary>
    /// <param name="model">Dati della segnalazione e del destinatario.</param>
    [HttpPost("mailsenderjob")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public IActionResult StartJobSendMail([FromBody] RichiestaSendMailViewModel model)
    {
        const string action = nameof(StartJobSendMail);
        var (scope, sw) = BeginRequestScope(action);
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (ValidateCommon(model, out var bad)) return bad!;

            var jobId = $"mail-{model.CodiceFiscalePartitaIva}-{model.Mail}";
            logger.LogInformation("Enqueue SendEmailJob cf:{CF}", model.CodiceFiscalePartitaIva);

            BackgroundJob.Enqueue<SendEmailJob>(x => x.SendEmailAsync(
                jobId, model.Cognome!, model.Mail, model.Nome,
                model.CodiceIpa, model.NomeEnte, model.DescrizioneProblema,
                model.TipologiaSegnalazione!, model.Telefono, model.CodiceFiscalePartitaIva!));

            var ok = Ok();
            LogEnd(action, sw, ok.StatusCode);
            return Ok(new { success = true });
        }
        finally { scope.Dispose(); }
    }

    /// <summary>
    /// POST api/mail/mailsenderjobanonimo — Accoda un job Hangfire di invio email per una segnalazione anonima.
    /// Identico a <see cref="StartJobSendMail"/> ma con prefisso jobId "mail-anonimo-".
    /// </summary>
    /// <param name="model">Dati della segnalazione anonima.</param>
    [HttpPost("mailsenderjobanonimo")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public IActionResult StartJobSendMailAnonimo([FromBody] RichiestaSendMailViewModel model)
    {
        const string action = nameof(StartJobSendMailAnonimo);
        var (scope, sw) = BeginRequestScope(action);
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (ValidateCommon(model, out var bad)) return bad!;

            var jobId = $"mail-anonimo-{model.CodiceFiscalePartitaIva}-{model.Mail}";
            BackgroundJob.Enqueue<SendEmailJob>(x => x.SendEmailAsync(
                jobId, model.Cognome!, model.Mail, model.Nome,
                model.CodiceIpa, model.NomeEnte, model.DescrizioneProblema,
                model.TipologiaSegnalazione!, model.Telefono, model.CodiceFiscalePartitaIva!));

            var ok = Ok();
            LogEnd(action, sw, ok.StatusCode);
            return ok;
        }
        finally { scope.Dispose(); }
    }

    /// <summary>
    /// POST api/mail/mailsenderjobcopilot — Accoda un job Hangfire di invio email per una segnalazione
    /// originata dal flusso Copilot. Prefisso jobId "mail-copilot-"; restituisce <c>{ success: true }</c>.
    /// </summary>
    /// <param name="model">Dati della segnalazione proveniente dal flusso Copilot.</param>
    [HttpPost("mailsenderjobcopilot")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public IActionResult StartJobSendMailCopilot([FromBody] RichiestaSendMailViewModel model)
    {
        const string action = nameof(StartJobSendMailCopilot);
        var (scope, sw) = BeginRequestScope(action);
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (ValidateCommon(model, out var bad)) return bad!;

            var jobId = $"mail-copilot-{model.CodiceFiscalePartitaIva}-{model.Mail}";
            logger.LogInformation("Enqueue SendEmailJob (copilot) cf:{CF}", model.CodiceFiscalePartitaIva);

            BackgroundJob.Enqueue<SendEmailJob>(x => x.SendEmailAsync(
                jobId, model.Cognome!, model.Mail, model.Nome,
                model.CodiceIpa, model.NomeEnte, model.DescrizioneProblema,
                model.TipologiaSegnalazione!, model.Telefono, model.CodiceFiscalePartitaIva!));

            var ok = Ok();
            LogEnd(action, sw, ok.StatusCode);
            return Ok(new { success = true });
        }
        finally { scope.Dispose(); }
    }

    /// <summary>
    /// GET api/mail/gettiposegnalazioni — Restituisce l'elenco delle tipologie di segnalazione
    /// configurate per l'ente identificato dal codice IPA.
    /// </summary>
    /// <param name="sendMailService">Servizio di invio email (iniettato da DI).</param>
    /// <param name="codiceIpaEnte">Codice IPA dell'ente di cui recuperare le tipologie.</param>
    [HttpGet("gettiposegnalazioni")]
    [ProducesResponseType(typeof(List<string>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<List<string>>> GetTipoSegnalazione(
        [FromServices] ISendEmailService sendMailService,
        string codiceIpaEnte)
    {
        const string action = nameof(GetTipoSegnalazione);
        var (scope, sw) = BeginRequestScope(action);
        try
        {
            var result = await sendMailService.GetTipologiaErroreAsync(codiceIpaEnte);
            var ok = Ok(result);
            LogEnd(action, sw, ok.StatusCode ?? 200);
            return ok;
        }
        finally { scope.Dispose(); }
    }

    /// <summary>
    /// GET api/mail/retrievesegnalazioni — Recupera le segnalazioni di un cittadino (per codice fiscale)
    /// arricchendole con i ticket CRM associati all'email del primo risultato.
    /// </summary>
    /// <param name="sendMailService">Servizio di recupero segnalazioni (iniettato da DI).</param>
    /// <param name="crmProxyService">Proxy verso il CRM esterno (iniettato da DI).</param>
    /// <param name="codiceFiscale">Codice fiscale o partita IVA del cittadino. Obbligatorio.</param>
    /// <param name="codiceIpaEnte">Filtro opzionale per codice IPA ente.</param>
    [HttpGet("retrievesegnalazioni")]
    [ProducesResponseType(typeof(List<RichiestaSendMailViewModel>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<List<RichiestaSendMailViewModel>>> RetrieveSegnalazioni(
        [FromServices] ISendEmailService sendMailService,
        [FromServices] ICrmProxyService crmProxyService,
        string codiceFiscale,
        string? codiceIpaEnte)
    {
        const string action = nameof(RetrieveSegnalazioni);
        var (scope, sw) = BeginRequestScope(action);
        try
        {
            if (string.IsNullOrWhiteSpace(codiceFiscale))
                return BadRequest("codiceFiscale is required.");

            var mailData = await sendMailService.RetrieveSegnalazioniAsync(codiceFiscale, codiceIpaEnte);
            List<CrmTicket> crmTickets = [];
            if (mailData.Count > 0 && !string.IsNullOrWhiteSpace(mailData[0].Mail))
            {
                var crmData = await crmProxyService.GetCrmDataAsync(mailData[0].Mail!);
                crmTickets = crmData?.Status == 200 ? crmData.Data ?? [] : [];
            }

            var result = mailData.Select(m => new RichiestaSendMailViewModel
            {
                Cognome = m.Cognome,
                Nome = m.Nome,
                Telefono = m.Telefono,
                Mail = m.Mail,
                TipologiaSegnalazione = m.TipologiaSegnalazione,
                DescrizioneProblema = m.DescrizioneProblema,
                CodiceIpa = m.CodiceEnte,
                CodiceFiscalePartitaIva = m.CodiceFiscalePartitaIva,
                NomeEnte = m.NomeEnte,
                DataRichiesta = m.LastModified,
                CrmTickets = crmTickets.Select(t => new CrmTicketViewModel
                {
                    Id = t.Id,
                    Ticket_Title = t.Ticket_Title,
                    Solution = t.Solution
                }).ToList()
            }).ToList();

            var ok = Ok(result);
            LogEnd(action, sw, ok.StatusCode ?? 200);
            return ok;
        }
        finally { scope.Dispose(); }
    }

    /// <summary>
    /// GET api/mail/segnalazioni/{id}/email — Restituisce il corpo HTML dell'email
    /// associata alla segnalazione con l'id specificato, con content-type <c>text/html</c>.
    /// Restituisce 404 se la segnalazione non esiste o non ha HTML salvato.
    /// </summary>
    /// <param name="dbContext">Contesto EF Core (iniettato da DI).</param>
    /// <param name="id">GUID della segnalazione.</param>
    [HttpGet("segnalazioni/{id:guid}/email")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetEmailHtml(
        [FromServices] DemoDbContext dbContext,
        Guid id)
    {
        var html = await dbContext.MailData
            .Where(m => m.Id == id)
            .Select(m => m.CorpoHtml)
            .FirstOrDefaultAsync();

        if (html == null) return NotFound();
        return Content(html, "text/html");
    }

    /// <summary>
    /// GET api/mail/segnalazioni — Restituisce le ultime segnalazioni presenti nel database,
    /// ordinate per data di modifica decrescente. Supporta filtro opzionale per codice IPA
    /// e limite massimo di risultati (default 100).
    /// </summary>
    /// <param name="dbContext">Contesto EF Core (iniettato da DI).</param>
    /// <param name="limit">Numero massimo di risultati da restituire (default 100).</param>
    /// <param name="codiceIpa">Filtro opzionale per codice IPA ente.</param>
    [HttpGet("segnalazioni")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetSegnalazioni(
        [FromServices] DemoDbContext dbContext,
        [FromQuery] int limit = 100,
        [FromQuery] string? codiceIpa = null)
    {
        var q = dbContext.MailData.OrderByDescending(m => m.LastModified).AsQueryable();
        if (!string.IsNullOrEmpty(codiceIpa))
            q = q.Where(m => m.CodiceEnte == codiceIpa);

        var data = await q.Take(limit).Select(m => new
        {
            m.Id,
            m.Cognome,
            m.Nome,
            m.Mail,
            m.TipologiaSegnalazione,
            m.CodiceEnte,
            m.NomeEnte,
            m.LastModified,
            m.CodiceFiscalePartitaIva,
            m.DescrizioneProblema
        }).ToListAsync();

        return Ok(data);
    }

    /// <summary>
    /// GET api/mail/getcrmdata — Interroga direttamente il CRM esterno tramite email utente
    /// e restituisce i ticket associati. Propaga lo status code del CRM in caso di errore.
    /// </summary>
    /// <param name="crmProxyService">Proxy verso il CRM esterno (iniettato da DI).</param>
    /// <param name="emailUtente">Email dell'utente di cui recuperare i ticket CRM. Obbligatoria.</param>
    [HttpGet("getcrmdata")]
    [ProducesResponseType(typeof(List<CrmTicket>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetCrmData(
        [FromServices] ICrmProxyService crmProxyService,
        string emailUtente)
    {
        if (string.IsNullOrWhiteSpace(emailUtente))
            return BadRequest("email utente is required");

        var result = await crmProxyService.GetCrmDataAsync(emailUtente);
        if (result == null) return NotFound();
        return result.Status == 200 ? Ok(result.Data ?? []) : StatusCode(result.Status, result.Data);
    }

    /// <summary>
    /// Esegue le validazioni comuni a tutti gli endpoint di accodamento mail:
    /// verifica che Cognome, TipologiaSegnalazione e CodiceFiscalePartitaIva siano valorizzati.
    /// </summary>
    /// <param name="model">Modello della richiesta da validare.</param>
    /// <param name="bad">
    /// Output: il <see cref="IActionResult"/> di errore (BadRequest) da restituire se la validazione fallisce,
    /// oppure <c>null</c> se la validazione è superata.
    /// </param>
    /// <returns><c>true</c> se la validazione fallisce (il chiamante deve restituire <paramref name="bad"/>); <c>false</c> se tutto è valido.</returns>
    private bool ValidateCommon(RichiestaSendMailViewModel model, out IActionResult? bad)
    {
        if (string.IsNullOrWhiteSpace(model.Cognome))
        {
            bad = BadRequest("Cognome is required.");
            return true;
        }
        if (string.IsNullOrWhiteSpace(model.TipologiaSegnalazione))
        {
            bad = BadRequest("TipologiaSegnalazione is required.");
            return true;
        }
        if (string.IsNullOrWhiteSpace(model.CodiceFiscalePartitaIva))
        {
            bad = BadRequest("CodiceFiscalePartitaIva is required.");
            return true;
        }
        bad = null;
        return false;
    }
}
