using InnovAI.Demo.Api.DTO;
using InnovAI.Demo.Api.Services;

namespace InnovAI.Demo.Api.Interfaces;

public interface IAiAgentCrmService
{
    /// <summary>
    /// Cerca nel CRM i ticket correlati alle segnalazioni simili trovate dalla ricerca vettoriale
    /// e restituisce il risultato aggregato con ticket id e soluzione per ciascuna segnalazione.
    /// </summary>
    /// <param name="segnalazioniSimili">Risultati della ricerca vettoriale con descrizione, codice e soluzione.</param>
    /// <param name="codiceEnte">Codice identificativo dell'ente (usato per il logging).</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    /// <returns><see cref="RisultatoSegnalazioniSimili"/> con l'esito e le segnalazioni arricchite.</returns>
    Task<RisultatoSegnalazioniSimili> TrovaSegnalazioniSimiliCrmAsync(
        IReadOnlyList<VectorSearchService.SimilarTextResult> segnalazioniSimili,
        string codiceEnte,
        CancellationToken cancellationToken = default);
}
