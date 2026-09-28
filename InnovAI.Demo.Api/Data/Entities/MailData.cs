using System.ComponentModel.DataAnnotations.Schema;

namespace InnovAI.Demo.Api.Data.Entities;

public class MailData : BaseEntity
{
    public string JobId { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Mail { get; set; }
    public string TipologiaSegnalazione { get; set; } = string.Empty;
    public string? DescrizioneProblema { get; set; }
    [Column("CodiceIpa")]
    public string CodiceEnte { get; set; } = string.Empty;
    public string NomeEnte { get; set; } = string.Empty;
    public string CodiceFiscalePartitaIva { get; set; } = string.Empty;
    public string Oggetto { get; set; } = string.Empty;
    public string? SentimentJson { get; set; }
    public string? CorpoHtml { get; set; }
}
