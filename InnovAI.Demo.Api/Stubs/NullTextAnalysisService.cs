using InnovAI.Demo.Api.DTO;
using InnovAI.Demo.Api.Interfaces;

namespace InnovAI.Demo.Api.Stubs;

/// <summary>
/// Stub no-op: restituisce sentiment neutro senza chiamare Azure Text Analytics.
/// Semplifica il setup della demo.
/// </summary>
public class NullTextAnalysisService : ITextAnalysisService
{
    public Task<SentimentAnalysisResultDTO> AnalyzeSentimentAsync(List<string> documents)
    {
        var result = new SentimentAnalysisResultDTO
        {
            IsSuccess = true,
            Documents = documents.Select(_ => new DocumentSentimentDTO
            {
                DocumentSentiment = SentimentType.Neutral,
                ConfidenceScores = new ConfidenceScoresDTO { Neutral = 1.0 }
            }).ToList()
        };
        return Task.FromResult(result);
    }
}
