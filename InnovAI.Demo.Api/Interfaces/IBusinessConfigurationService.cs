using InnovAI.Demo.Api.DTO;

namespace InnovAI.Demo.Api.Interfaces;

public interface IBusinessConfigurationService
{
    Task<BusinessServiceConfiguration> GetBusinessServiceConfiguration(string codice, string urlCallbackClient);
}
