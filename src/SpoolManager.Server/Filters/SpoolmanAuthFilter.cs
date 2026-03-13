using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Server.Filters;

public class SpoolmanAuthFilter : IAsyncActionFilter
{
    private readonly ISpoolmanApiKeyRepository _apiKeys;

    public SpoolmanAuthFilter(ISpoolmanApiKeyRepository apiKeys) => _apiKeys = apiKeys;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? apiKey = null;

        if (context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var headerValue))
            apiKey = headerValue.ToString();

        if (string.IsNullOrEmpty(apiKey) && context.HttpContext.Request.Query.TryGetValue("apikey", out var queryValue))
            apiKey = queryValue.ToString();

        if (string.IsNullOrEmpty(apiKey))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "X-Api-Key header or apikey query parameter is required." });
            return;
        }

        var key = await _apiKeys.GetByApiKeyAsync(apiKey);
        if (key == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid API key." });
            return;
        }

        context.HttpContext.Items["SpoolmanProjectId"] = key.ProjectId;

        _ = Task.Run(async () =>
        {
            try { await _apiKeys.UpdateLastUsedAsync(key.Id); } catch { }
        });

        await next();
    }
}
