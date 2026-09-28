using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using InnovAI.Demo.Api.DTO;
using InnovAI.Demo.Api.Interfaces;
using InnovAI.Demo.Api.Settings;

namespace InnovAI.Demo.Api.Services;

/// <summary>
/// Implementazione reale di ICrmProxyService.
/// Chiama le API REST del CRM usando le credenziali in CrmApi (appsettings / user-secrets).
/// Attiva quando Demo:UseMockCrm = false.
/// </summary>
public class CrmProxyService(
    IHttpClientFactory httpClientFactory,
    IOptions<CrmApiSettings> options,
    ILogger<CrmProxyService> logger) : ICrmProxyService
{
    private readonly CrmApiSettings _settings = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CrmDataResponse?> GetCrmDataAsync(string emailUtente)
    {
        var url = BuildUrl(_settings.EmailQueryParam, emailUtente);
        logger.LogInformation("CrmProxyService.GetCrmDataAsync → {Url}", url);
        return await GetAsync(url);
    }

    public async Task<CrmDataResponse?> GetCrmTicketsByKeywordsAsync(string keywords)
    {
        var url = BuildUrl(_settings.KeywordsQueryParam, keywords);
        logger.LogInformation("CrmProxyService.GetCrmTicketsByKeywordsAsync → {Url}", url);
        return await GetAsync(url);
    }

    private string BuildUrl(string paramName, string paramValue)
        => $"{_settings.BaseUrl.TrimEnd('/')}?{paramName}={Uri.EscapeDataString(paramValue)}";

    private async Task<CrmDataResponse?> GetAsync(string url)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("CrmApi");
            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CrmProxyService: risposta non-OK {Status} da {Url}", (int)response.StatusCode, url);
                return new CrmDataResponse { Status = (int)response.StatusCode, Data = [] };
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CrmDataResponse>(json, JsonOpts);

            logger.LogInformation("CrmProxyService: {Count} ticket ricevuti",
                result?.Data?.Count ?? 0);

            return result ?? new CrmDataResponse { Status = 200, Data = [] };
        }
        catch (TaskCanceledException)
        {
            logger.LogError("CrmProxyService: timeout su {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CrmProxyService: errore su {Url}", url);
            return null;
        }
    }
}
