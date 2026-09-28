namespace InnovAI.Demo.Api.DTO;

public record SegnalazioneSimileInfo(
    string Descrizione,
    string? TicketCrmId,
    string? SoluzioneCrm = null);

public record RisultatoSegnalazioniSimili(
    bool TrovateSimili,
    IReadOnlyList<SegnalazioneSimileInfo> Segnalazioni);
