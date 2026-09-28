using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using InnovAI.Demo.Api.Settings;

namespace InnovAI.Demo.Api.Filters;

/// <summary>
/// Filtro API Key per endpoint pubblici (es. /api/similarity).
/// Copilot Studio invia: Authorization: Bearer {apiKey}  oppure  X-Api-Key: {apiKey}
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var settings = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<DemoSettings>>().Value;

        var expectedKey = settings.ApiKey;

        // Accetta sia X-Api-Key header sia Bearer token
        string? providedKey = null;
        if (context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var headerKey))
            providedKey = headerKey.FirstOrDefault();

        if (providedKey == null &&
            context.HttpContext.Request.Headers.TryGetValue("Authorization", out var auth))
        {
            var value = auth.FirstOrDefault() ?? string.Empty;
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                providedKey = value["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(providedKey) || providedKey != expectedKey)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "API key non valida o mancante" });
            return;
        }

        await next();
    }
}
