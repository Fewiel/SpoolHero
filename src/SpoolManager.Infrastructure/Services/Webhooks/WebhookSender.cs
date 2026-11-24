using System.Text;
using Microsoft.Extensions.Logging;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services.Webhooks;

public record WebhookDeliveryResult(bool Success, int? StatusCode, string? Error);

public interface IWebhookSender
{
    Task<WebhookDeliveryResult> SendAsync(Webhook hook, WebhookEventData evt, string? projectName, CancellationToken ct = default);
}

public class WebhookSender : IWebhookSender
{
    private readonly HttpClient _http;
    private readonly WebhookOptions _options;
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(HttpClient http, WebhookOptions options, ILogger<WebhookSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<WebhookDeliveryResult> SendAsync(Webhook hook, WebhookEventData evt, string? projectName, CancellationToken ct = default)
    {
        try
        {
            var (url, body) = BuildRequest(hook, evt, projectName);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, ct);
            var code = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return new WebhookDeliveryResult(true, code, null);

            var responseText = await response.Content.ReadAsStringAsync(ct);
            if (responseText.Length > 300)
                responseText = responseText[..300];
            var error = $"HTTP {code} {response.ReasonPhrase}".Trim();
            if (!string.IsNullOrWhiteSpace(responseText))
                error += ": " + responseText;
            _logger.LogWarning("Webhook {WebhookId} ({Type}) returned HTTP {Status}", hook.Id, hook.Type, code);
            return new WebhookDeliveryResult(false, code, Scrub(error, hook));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var message = ex is TaskCanceledException
                ? $"Timeout after {_options.TimeoutSeconds}s."
                : ex.Message;
            _logger.LogWarning("Webhook {WebhookId} ({Type}) failed: {Error}", hook.Id, hook.Type, Scrub(message, hook));
            return new WebhookDeliveryResult(false, null, Scrub(message, hook));
        }
    }

    private (string Url, string Body) BuildRequest(Webhook hook, WebhookEventData evt, string? projectName)
    {
        switch (hook.Type)
        {
            case WebhookTypes.Telegram:
                if (!WebhookSecurity.IsValidBotToken(hook.BotToken) || !WebhookSecurity.IsValidChatId(hook.ChatId))
                    throw new InvalidOperationException("Telegram bot token or chat id is missing or invalid.");
                var baseUrl = _options.TelegramApiBaseUrl.TrimEnd('/');
                return ($"{baseUrl}/bot{hook.BotToken!.Trim()}/sendMessage",
                    WebhookPayloadBuilder.BuildTelegram(evt, projectName, hook.ChatId!.Trim()));

            case WebhookTypes.Discord:
                return (RequireUrl(hook), WebhookPayloadBuilder.BuildDiscord(evt, projectName));

            case WebhookTypes.HomeAssistant:
            case WebhookTypes.Generic:
                return (RequireUrl(hook), WebhookPayloadBuilder.BuildStructured(evt, projectName));

            default:
                throw new InvalidOperationException($"Unknown webhook type '{hook.Type}'.");
        }
    }

    private static string RequireUrl(Webhook hook)
    {
        if (!WebhookSecurity.IsValidUrl(hook.Url, out var error))
            throw new InvalidOperationException(error);
        return hook.Url!.Trim();
    }

    // Never let secrets end up in stored error messages or logs.
    private static string Scrub(string message, Webhook hook)
    {
        if (!string.IsNullOrEmpty(hook.BotToken))
            message = message.Replace(hook.BotToken, "***");
        return message;
    }
}
