using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Infrastructure.Services;
using SpoolManager.Infrastructure.Services.Webhooks;
using SpoolManager.Server.Filters;
using SpoolManager.Shared.DTOs.Webhooks;
using SpoolManager.Shared.Models;

namespace SpoolManager.Server.Controllers;

[ApiController]
[Route("api/webhooks")]
[Authorize]
[ServiceFilter(typeof(ProjectAuthFilter))]
public class WebhooksController : ControllerBase
{
    private const int MaxWebhooksPerProject = 20;

    private readonly IWebhookRepository _webhooks;
    private readonly IProjectRepository _projects;
    private readonly IWebhookSender _sender;
    private readonly IAuditService _audit;
    private ProjectMember ProjectMember => (ProjectMember)HttpContext.Items["ProjectMember"]!;
    private string? UserName => User.FindFirst(ClaimTypes.Name)?.Value;
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    public WebhooksController(IWebhookRepository webhooks, IProjectRepository projects, IWebhookSender sender, IAuditService audit)
    {
        _webhooks = webhooks;
        _projects = projects;
        _sender = sender;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var hooks = await _webhooks.GetAllAsync(ProjectMember.ProjectId);
        return Ok(hooks.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var hook = await GetOwnAsync(id);
        return hook == null ? NotFound() : Ok(MapToDto(hook));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveWebhookRequest request)
    {
        if ((await _webhooks.GetAllAsync(ProjectMember.ProjectId)).Count >= MaxWebhooksPerProject)
            return BadRequest(new { message = $"A project can have at most {MaxWebhooksPerProject} webhooks." });

        var hook = new Webhook { ProjectId = ProjectMember.ProjectId };
        var error = Apply(hook, request, isNew: true);
        if (error != null)
            return BadRequest(new { message = error });

        var id = await _webhooks.CreateAsync(hook);
        await LogAsync("webhook.create", hook);
        return CreatedAtAction(nameof(GetById), new { id }, MapToDto((await _webhooks.GetByIdAsync(id))!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, SaveWebhookRequest request)
    {
        var hook = await GetOwnAsync(id);
        if (hook == null)
            return NotFound();
        var error = Apply(hook, request, isNew: false);
        if (error != null)
            return BadRequest(new { message = error });
        hook.UpdatedAt = DateTime.UtcNow;
        await _webhooks.UpdateAsync(hook);
        await LogAsync("webhook.update", hook);
        return Ok(MapToDto((await _webhooks.GetByIdAsync(id))!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (ProjectMember.Role != "admin")
            return StatusCode(403, new { message = "Only admins can delete." });
        var hook = await GetOwnAsync(id);
        if (hook == null)
            return NotFound();
        await _webhooks.DeleteAsync(id);
        await LogAsync("webhook.delete", hook);
        return NoContent();
    }

    /// <summary>Sends a test event synchronously so the user gets immediate feedback.</summary>
    [HttpPost("{id}/test")]
    public async Task<IActionResult> Test(Guid id)
    {
        var hook = await GetOwnAsync(id);
        if (hook == null)
            return NotFound();
        var projectName = (await _projects.GetByIdAsync(ProjectMember.ProjectId))?.Name;
        var result = await _sender.SendAsync(hook, WebhookEventData.ForTest(hook.ProjectId), projectName, HttpContext.RequestAborted);
        await _webhooks.UpdateDeliveryStatusAsync(hook.Id, DateTime.UtcNow, result.Success, result.StatusCode, result.Error);
        return Ok(new WebhookTestResult { Success = result.Success, StatusCode = result.StatusCode, Error = result.Error });
    }

    private async Task<Webhook?> GetOwnAsync(Guid id)
    {
        var hook = await _webhooks.GetByIdAsync(id);
        return hook == null || hook.ProjectId != ProjectMember.ProjectId ? null : hook;
    }

    private static string? Apply(Webhook hook, SaveWebhookRequest r, bool isNew)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 200)
            return "Name is required (max. 200 characters).";
        var type = r.Type?.Trim().ToLowerInvariant() ?? "";
        if (!WebhookTypes.All.Contains(type))
            return "Unknown webhook type.";

        hook.Name = name;
        hook.Type = type;
        hook.IsEnabled = r.IsEnabled;
        hook.OnDryerFinished = r.OnDryerFinished;
        hook.OnSpoolLow = r.OnSpoolLow;
        hook.OnSpoolConsumed = r.OnSpoolConsumed;

        if (type == WebhookTypes.Telegram)
        {
            // Keep the stored token when the client sends nothing or the masked placeholder back.
            var token = r.BotToken?.Trim();
            var keepToken = string.IsNullOrEmpty(token) || token.Contains('•') || token == WebhookSecurity.MaskToken(hook.BotToken);
            if (!keepToken)
            {
                if (!WebhookSecurity.IsValidBotToken(token))
                    return "Invalid Telegram bot token (expected format 123456789:ABC...).";
                hook.BotToken = token;
            }
            else if (isNew || string.IsNullOrEmpty(hook.BotToken))
                return "Telegram bot token is required.";

            if (!WebhookSecurity.IsValidChatId(r.ChatId))
                return "Invalid Telegram chat id (numeric id or @channelname).";
            hook.ChatId = r.ChatId!.Trim();
            hook.Url = null;
        }
        else
        {
            if (!WebhookSecurity.IsValidUrl(r.Url, out var urlError))
                return urlError;
            hook.Url = r.Url!.Trim();
            hook.BotToken = null;
            hook.ChatId = null;
        }
        return null;
    }

    private Task LogAsync(string action, Webhook hook) =>
        _audit.LogAsync(action,
            userId: UserId, username: UserName,
            entityType: "webhook", entityId: hook.Id.ToString(), entityName: hook.Name,
            projectId: ProjectMember.ProjectId,
            details: hook.Type,
            ipAddress: ClientIp);

    private static WebhookDto MapToDto(Webhook w) => new()
    {
        Id = w.Id,
        ProjectId = w.ProjectId,
        Name = w.Name,
        Type = w.Type,
        Url = w.Url,
        ChatId = w.ChatId,
        HasBotToken = !string.IsNullOrEmpty(w.BotToken),
        BotTokenMasked = WebhookSecurity.MaskToken(w.BotToken),
        IsEnabled = w.IsEnabled,
        OnDryerFinished = w.OnDryerFinished,
        OnSpoolLow = w.OnSpoolLow,
        OnSpoolConsumed = w.OnSpoolConsumed,
        LastTriggeredAt = w.LastTriggeredAt,
        LastSuccess = w.LastSuccess,
        LastStatusCode = w.LastStatusCode,
        LastError = w.LastError,
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.UpdatedAt
    };
}
