using InnovAI.Demo.Api.DTO;

namespace InnovAI.Demo.Api.Interfaces;

public interface ITextAnalysisService
{
    Task<SentimentAnalysisResultDTO> AnalyzeSentimentAsync(List<string> documents);
}
