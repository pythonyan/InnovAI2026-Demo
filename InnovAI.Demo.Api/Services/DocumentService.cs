using Microsoft.EntityFrameworkCore;
using InnovAI.Demo.Api.Data;

namespace InnovAI.Demo.Api.Services;

public record DocumentModel(Guid Id, string Name, DateTimeOffset CreationDate, int ChunkCount);
public record DocumentChunkModel(Guid Id, int Index, string Content, float[]? Embedding = null);

public class DocumentService(DemoDbContext dbContext)
{
    public async Task<IEnumerable<DocumentModel>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Documents
            .OrderBy(d => d.Name)
            .Select(d => new DocumentModel(d.Id, d.Name, d.CreationDate, d.Chunks.Count))
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
        => dbContext.Documents.Where(d => d.Id == documentId).ExecuteDeleteAsync(cancellationToken);
}
