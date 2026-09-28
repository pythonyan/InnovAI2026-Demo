using System.ComponentModel.DataAnnotations;

namespace InnovAI.Demo.Api.ViewModels;

/// <summary>
/// Request body per POST /api/similarity — consumato da Copilot Studio.
/// </summary>
public class SimilarityRequest
{
    [Required]
    public string Testo { get; set; } = string.Empty;

    public string? Categoria { get; set; }

    [Range(1, 10)]
    public int TopN { get; set; } = 3;
}

/// <summary>
/// Response di POST /api/similarity.
/// </summary>
public class SimilarityResponse
{
    public List<SimilarityTicket> Tickets { get; set; } = [];
}

public class SimilarityTicket
{
    public string TicketId { get; set; } = string.Empty;
    public string Titolo { get; set; } = string.Empty;
    public string? Soluzione { get; set; }
    public double Similarity { get; set; }
}
