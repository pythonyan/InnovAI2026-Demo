using System.Text;
using Microsoft.Extensions.Logging;
using InnovAI.Demo.Api.DTO;

namespace InnovAI.Demo.Api.Services;

public class TemplateProcessor(ILogger<TemplateProcessor> logger)
{
    public (string htmlContent, string plainText) SubstitutePlaceholders(
        string filePath,
        string cognome,
        string? email,
        string? nome,
        string? descrizioneProblema,
        string? telefono,
        string tipologiaSegnalazione,
        SentimentAnalysisResultDTO? sentimentResult)
    {
        try
        {
            string tableResult = sentimentResult != null ? GenerateSentimentTable(sentimentResult) : string.Empty;
            string plainTextResult = sentimentResult != null ? GenerateSentimentPlainText(sentimentResult) : string.Empty;

            string template = File.ReadAllText(filePath);

            string htmlContent = template
                .Replace("{Cognome}", System.Web.HttpUtility.HtmlEncode(cognome))
                .Replace("{Nome}", System.Web.HttpUtility.HtmlEncode(nome))
                .Replace("{Email}", System.Web.HttpUtility.HtmlEncode(email ?? ""))
                .Replace("{Telefono}", System.Web.HttpUtility.HtmlEncode(telefono ?? ""))
                .Replace("{tipologia_segnalazione}", System.Web.HttpUtility.HtmlEncode(tipologiaSegnalazione))
                .Replace("{DescrizioneProblema}", System.Web.HttpUtility.HtmlEncode(descrizioneProblema ?? "").Replace(Environment.NewLine, "<br>"))
                .Replace("{Sentiment}", tableResult);

            string plainText = template
                .Replace("{Cognome}", cognome)
                .Replace("{Nome}", nome)
                .Replace("{Email}", email ?? "")
                .Replace("{Telefono}", telefono ?? "")
                .Replace("{tipologia_segnalazione}", tipologiaSegnalazione)
                .Replace("{DescrizioneProblema}", descrizioneProblema ?? "")
                .Replace("{Sentiment}", plainTextResult);

            if (!htmlContent.Contains("<html>"))
            {
                htmlContent = $@"<html><head><meta charset='UTF-8'></head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
{htmlContent}
</body></html>";
            }

            return (htmlContent, plainText);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TemplateProcessor: errore durante elaborazione template");
            throw;
        }
    }

    private static string GenerateSentimentTable(SentimentAnalysisResultDTO result)
    {
        if (!result.IsSuccess || result.Documents.Count == 0)
            return "<div>Sentiment: N/D</div>";

        var doc = result.Documents[0];
        return $"<div style='padding:8px;background:#f5f5f5;border-radius:4px;'>" +
               $"<strong>Sentiment:</strong> {doc.DocumentSentiment}" +
               $" (Positivo: {doc.ConfidenceScores.Positive:P0} | Neutro: {doc.ConfidenceScores.Neutral:P0} | Negativo: {doc.ConfidenceScores.Negative:P0})" +
               $"</div>";
    }

    private static string GenerateSentimentPlainText(SentimentAnalysisResultDTO result)
    {
        if (!result.IsSuccess || result.Documents.Count == 0) return "Sentiment: N/D";
        var doc = result.Documents[0];
        return $"Sentiment: {doc.DocumentSentiment}";
    }
}
