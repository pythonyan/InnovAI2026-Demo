using System.Text.Json;
using System.Text.Json.Serialization;

namespace InnovAI.Demo.Api.ViewModels;

public class RichiestaSendMailViewModel
{
    public string Cognome { get; set; } = string.Empty;
    public string? Nome { get; set; }
    public string? Telefono { get; set; }
    public string? Mail { get; set; }
    public string TipologiaSegnalazione { get; set; } = string.Empty;
    public string? DescrizioneProblema { get; set; }
    public string? CodiceIpa { get; set; }
    public string NomeEnte { get; set; } = string.Empty;
    public string? CodiceFiscalePartitaIva { get; set; }
    public DateTime? DataRichiesta { get; set; }

    [JsonConverter(typeof(CrmTicketsLenientConverter))]
    public List<CrmTicketViewModel>? CrmTickets { get; set; }
}

/// <summary>
/// Deserializza CrmTickets tollerando string/null al posto di array
/// (Copilot Studio può mandare espressioni Power Fx non valutate).
/// </summary>
public class CrmTicketsLenientConverter : JsonConverter<List<CrmTicketViewModel>?>
{
    public override List<CrmTicketViewModel>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }
        return JsonSerializer.Deserialize<List<CrmTicketViewModel>>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, List<CrmTicketViewModel>? value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}
public class CrmTicketViewModel
{
    // struttura CS
    public string? TicketId { get; set; }
    public string? Titolo { get; set; }
    public string? Soluzione { get; set; }

    // struttura originale
    public string? Id { get; set; }
    public string? Ticket_Title { get; set; }
    public string? Solution { get; set; }

    // helper per normalizzare
    public string GetId() => Id ?? TicketId ?? "N/D";
    public string GetTitle() => Ticket_Title ?? Titolo ?? "N/D";
    public string GetSolution() => Solution ?? Soluzione ?? "N/D";
}