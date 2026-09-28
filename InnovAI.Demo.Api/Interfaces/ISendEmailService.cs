using InnovAI.Demo.Api.Data.Entities;

namespace InnovAI.Demo.Api.Interfaces;

public interface ISendEmailService
{
    Task<bool> SendEmailAsync(
        string jobId,
        string cognome,
        string? email,
        string? nome,
        string? codiceEnte,
        string? nomeEnte,
        string? descrizioneProblema,
        string tipologiaSegnalazione,
        string? telefono,
        string codiceFiscalePartitaIva);

    Task<bool> SendEmailWithoutJobAsync(
        string cognome,
        string? email,
        string? nome,
        string? codiceEnte,
        string? nomeEnte,
        string? descrizioneProblema,
        string tipologiaSegnalazione,
        string? telefono,
        string codiceFiscalePartitaIva);

    Task<List<string>> GetTipologiaErroreAsync(string codiceIpa);
    Task<Guid> SaveEmailDataAsync(MailData mailData);
    Task<List<MailData>> RetrieveSegnalazioniAsync(string codiceFiscale, string? codiceIpa);
}
