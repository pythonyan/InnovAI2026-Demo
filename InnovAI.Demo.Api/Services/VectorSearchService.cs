using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using InnovAI.Demo.Api.Data;
using InnovAI.Demo.Api.Data.Entities;

namespace InnovAI.Demo.Api.Services;

public class VectorSearchService(
    DemoDbContext dbContext,
    DocumentService documentService,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    TokenizerService tokenizerService,
    TimeProvider timeProvider,
    ILogger<VectorSearchService> logger)
{
    public class SimilarTextResult
    {
        public Guid DocumentId { get; set; }
        public string? DocumentName { get; set; }
        public string? Descrizione { get; set; }
        public string? CodiceSegnalazione { get; set; }
        public string? Soluzione { get; set; }
        public double Similarity { get; set; }
    }

    /// <summary>
    /// Importa un documento JSON (segnalazione) nel DB con embedding vettoriale.
    /// Usato da SendEmailJob dopo l'invio della mail.
    /// </summary>
    public async Task<(Guid documentId, int tokenCount)> ImportAsync(
        Stream stream,
        string name,
        string contentType,
        Guid? documentId,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var tokenCount = tokenizerService.CountEmbeddingTokens(content);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var document = await strategy.ExecuteAsync(async ct =>
        {
            await dbContext.Database.BeginTransactionAsync(ct);

            if (documentId.HasValue)
                await documentService.DeleteAsync(documentId.Value, ct);

            var doc = new Document
            {
                Id = documentId.GetValueOrDefault(),
                Name = name,
                CreationDate = timeProvider.GetUtcNow()
            };
            dbContext.Documents.Add(doc);

            // Per le segnalazioni il content è un singolo chunk
            var embeddings = await embeddingGenerator.GenerateAndZipAsync([content], cancellationToken: ct);
            foreach (var (idx, emb) in embeddings.Select((e, i) => (i, e)))
            {
                dbContext.DocumentChunks.Add(new DocumentChunk
                {
                    Document = doc,
                    Index = idx,
                    Content = emb.Value,
                    Embedding = new SqlVector<float>(emb.Embedding.Vector)
                });
            }

            await dbContext.SaveChangesAsync(ct);
            await dbContext.Database.CommitTransactionAsync(ct);
            return doc;
        }, cancellationToken);

        return (document.Id, tokenCount);
    }

    /// <summary>
    /// Ricerca le segnalazioni più simili tramite VECTOR_DISTANCE su SQL Server 2022+.
    /// </summary>
    public async Task<List<SimilarTextResult>> FindSimilarTexts(
        string queryText,
        int maxResults = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryEmbedding = (await embeddingGenerator.GenerateVectorAsync(queryText, cancellationToken: cancellationToken)).ToArray();
            var embeddingStr = $"[{string.Join(",", queryEmbedding.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";

            var sql = $@"
                DECLARE @q VECTOR({queryEmbedding.Length}) = CAST('{embeddingStr}' AS VECTOR({queryEmbedding.Length}));
                SELECT TOP {maxResults}
                    dc.DocumentId,
                    d.Name as DocumentName,
                    dc.Content,
                    VECTOR_DISTANCE('cosine', dc.Embedding, @q) as Distance
                FROM DocumentChunks dc
                INNER JOIN Documents d ON dc.DocumentId = d.Id
                ORDER BY VECTOR_DISTANCE('cosine', dc.Embedding, @q) ASC";

            var conn = dbContext.Database.GetDbConnection();
            await dbContext.Database.OpenConnectionAsync(cancellationToken);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            var results = new List<SimilarTextResult>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var docId = reader.GetGuid(reader.GetOrdinal("DocumentId"));
                var docName = reader.GetString(reader.GetOrdinal("DocumentName"));
                var rawContent = reader.GetString(reader.GetOrdinal("Content"));
                var distance = reader.GetDouble(reader.GetOrdinal("Distance"));

                string? descrizione = null;
                string? codice = null;
                string? soluzione = null;
                try
                {
                    using var doc = JsonDocument.Parse(rawContent);
                    if (doc.RootElement.TryGetProperty("Descrizione", out var d)) descrizione = d.GetString();
                    else if (doc.RootElement.TryGetProperty("DescrizioneProblema", out var dp)) descrizione = dp.GetString();
                    if (doc.RootElement.TryGetProperty("CodiceSegnalazione", out var c)) codice = c.GetString();
                    if (doc.RootElement.TryGetProperty("Soluzione", out var s)) soluzione = s.GetString();
                }
                catch { descrizione = rawContent; }

                results.Add(new SimilarTextResult
                {
                    DocumentId = docId,
                    DocumentName = docName,
                    Descrizione = descrizione,
                    CodiceSegnalazione = codice,
                    Soluzione = soluzione,
                    Similarity = Math.Max(0, 1.0 - distance)
                });
            }

            logger.LogInformation("FindSimilarTexts: trovati {Count} risultati per la query", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Errore in FindSimilarTexts per query '{Query}'", queryText);
            throw;
        }
    }
}
