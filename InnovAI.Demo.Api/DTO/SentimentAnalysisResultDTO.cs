using System.Text.Json.Serialization;

namespace InnovAI.Demo.Api.DTO;

public class SentimentAnalysisResultDTO
{
    public bool IsSuccess { get; set; }
    public List<DocumentSentimentDTO> Documents { get; set; } = [];
    public string DetectedLanguage { get; set; } = string.Empty;
}

public class DocumentSentimentDTO
{
    public SentimentType DocumentSentiment { get; set; }
    public ConfidenceScoresDTO ConfidenceScores { get; set; } = new();
    public List<SentenceSentimentDTO> Sentences { get; set; } = [];
}

public class SentenceSentimentDTO
{
    public string Text { get; set; } = string.Empty;
    public SentimentType Sentiment { get; set; }
    public ConfidenceScoresDTO ConfidenceScores { get; set; } = new();
    public List<OpinionDTO> Opinions { get; set; } = [];
}

public class ConfidenceScoresDTO
{
    public double Positive { get; set; }
    public double Negative { get; set; }
    public double Neutral { get; set; } = 1.0;
}

public class OpinionDTO
{
    public SentimentTargetDTO Target { get; set; } = new();
    public List<AssessmentDTO> Assessments { get; set; } = [];
}

public class SentimentTargetDTO
{
    public string Text { get; set; } = string.Empty;
    public SentimentType Sentiment { get; set; }
    public ConfidenceScoresDTO ConfidenceScores { get; set; } = new();
}

public class AssessmentDTO
{
    public string Text { get; set; } = string.Empty;
    public SentimentType Sentiment { get; set; }
    public ConfidenceScoresDTO ConfidenceScores { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SentimentType { Positive, Negative, Neutral, Mixed }
