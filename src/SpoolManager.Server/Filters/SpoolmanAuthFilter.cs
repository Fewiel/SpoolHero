using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Server.Filters;

public class SpoolmanAuthFilter : IAsyncActionFilter
{
    private readonly ISpoolmanApiKeyRepository _apiKeys;
    private readonly ISpoolmanCallLogRepository _callLogs;

    public SpoolmanAuthFilter(ISpoolmanApiKeyRepository apiKeys, ISpoolmanCallLogRepository callLogs)
    {
        _apiKeys = apiKeys;
        _callLogs = callLogs;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var apiKeyValue = context.RouteData.Values["apiKey"]?.ToString();

        if (string.IsNullOrEmpty(apiKeyValue))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "API key missing from URL." });
            return;
        }

        var key = await _apiKeys.GetByApiKeyAsync(apiKeyValue);
        if (key == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid API key." });
            return;
        }

        context.HttpContext.Items["SpoolmanProjectId"] = key.ProjectId;
        context.HttpContext.Items["SpoolmanApiKeyId"] = key.Id;

        _ = Task.Run(async () =>
        {
            try { await _apiKeys.UpdateLastUsedAsync(key.Id); } catch { }
        });

        var result = await next();

        var statusCode = context.HttpContext.Response.StatusCode;
        var method = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path.Value ?? "/";

        _ = Task.Run(async () =>
        {
            try { await _callLogs.LogAsync(key.Id, method, path, statusCode); } catch { }
        });
    }
}
