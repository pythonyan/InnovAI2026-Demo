using InnovAI.Demo.Api.DTO;

namespace InnovAI.Demo.Api.Interfaces;

public interface ICrmProxyService
{
    Task<CrmDataResponse?> GetCrmDataAsync(string emailUtente);
    Task<CrmDataResponse?> GetCrmTicketsByKeywordsAsync(string keywords);
}
