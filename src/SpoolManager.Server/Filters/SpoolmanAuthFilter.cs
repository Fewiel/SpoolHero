using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SpoolManager.Infrastructure.Repositories;

namespace SpoolManager.Server.Filters;

// Marks Spoolman endpoints that work without a valid API key (Spoolman's /health is public).
[AttributeUsage(AttributeTargets.Method)]
public class SpoolmanPublicAttribute : Attribute;

// Spoolman clients (Moonraker etc.) cannot send JWTs or custom headers, so the API key is part of
// the URL path: /{apiKey}/api/v1/... The key resolves to the project whose spools are exposed.
public class SpoolmanAuthFilter : IAsyncActionFilter
{
    public const string ProjectIdItem = "SpoolmanProjectId";
    public const string ApiKeyIdItem = "SpoolmanApiKeyId";

    private readonly ISpoolmanApiKeyRepository _apiKeys;
    private readonly ISpoolmanCallLogRepository _callLogs;
    private readonly ILogger<SpoolmanAuthFilter> _logger;

    public SpoolmanAuthFilter(ISpoolmanApiKeyRepository apiKeys, ISpoolmanCallLogRepository callLogs, ILogger<SpoolmanAuthFilter> logger)
    {
        _apiKeys = apiKeys;
        _callLogs = callLogs;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isPublic = context.ActionDescriptor.EndpointMetadata.OfType<SpoolmanPublicAttribute>().Any();
        var apiKeyValue = context.RouteData.Values["apiKey"]?.ToString();

        var key = string.IsNullOrEmpty(apiKeyValue) ? null : await _apiKeys.GetByApiKeyAsync(apiKeyValue);
        if (key == null)
        {
            if (isPublic)
            {
                await next();
                return;
            }
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid or missing Spoolman API key." });
            return;
        }

        context.HttpContext.Items[ProjectIdItem] = key.ProjectId;
        context.HttpContext.Items[ApiKeyIdItem] = key.Id;

        var executed = await next();

        var statusCode = executed.Exception != null && !executed.ExceptionHandled
            ? StatusCodes.Status500InternalServerError
            : executed.Result is IStatusCodeActionResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;

        // Never store the key itself in the log: keep only the part after "/{apiKey}".
        var path = context.HttpContext.Request.Path.Value ?? "/";
        var prefix = "/" + apiKeyValue;
        if (path.StartsWith(prefix, StringComparison.Ordinal))
            path = path[prefix.Length..];

        await LogCallAsync(key.Id, context.HttpContext.Request.Method, path, statusCode);
    }

    private async Task LogCallAsync(Guid keyId, string method, string path, int statusCode)
    {
        try
        {
            await _apiKeys.UpdateLastUsedAsync(keyId);
            await _callLogs.LogAsync(keyId, method, path, statusCode);
            if (Random.Shared.Next(200) == 0)
                await _callLogs.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log Spoolman API call");
        }
    }
}
