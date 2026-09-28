using Microsoft.Data.SqlTypes;

namespace InnovAI.Demo.Api.Data.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int Index { get; set; }
    public required string Content { get; set; }
    public required SqlVector<float> Embedding { get; set; }
    public virtual Document Document { get; set; } = null!;
}
