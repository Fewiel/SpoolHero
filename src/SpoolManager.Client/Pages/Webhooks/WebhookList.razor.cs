using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;
using SpoolManager.Shared.DTOs.Webhooks;

namespace SpoolManager.Client.Pages.Webhooks;

public partial class WebhookList
{
    [Inject] private WebhookService Webhooks { get; set; } = default!;
    [Inject] private LocalizationService L { get; set; } = default!;
    [Inject] private ProjectService Project { get; set; } = default!;

    private static readonly string[] Types = ["discord", "telegram", "homeassistant", "generic"];

    private bool _loading = true;
    private bool _loadError;
    private bool _deleting;
    private Guid? _togglingId;
    private string? _notice;
    private bool _noticeSuccess;

    private bool IsAdmin => Project.CurrentProject?.MyRole == "admin";
    private List<WebhookDto> _webhooks = [];
    private SaveWebhookRequest? _form;
    private Guid? _editId;
    private string? _editTokenMasked;
    private string? _error;
    private bool _saving;
    private WebhookDto? _deleteTarget;
    private string? _deleteError;
    private Guid? _testingId;
    private Guid? _testTargetId;
    private string? _testMessage;
    private bool _testSuccess;

    protected override async Task OnInitializedAsync()
    {
        if (Project.CurrentProject == null)
        {
            _loading = false;
            return;
        }
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        // Only show the spinner on the first load; refreshes keep the cards on screen.
        _loading = _webhooks.Count == 0;
        _loadError = false;
        try
        {
            _webhooks = await Webhooks.GetAllAsync() ?? [];
        }
        catch (HttpRequestException)
        {
            _loadError = true;
        }
        _loading = false;
    }

    private void ShowAdd()
    {
        _editId = null;
        _editTokenMasked = null;
        _form = new SaveWebhookRequest { Type = "discord" };
        _error = null;
    }

    private void ShowEdit(WebhookDto w)
    {
        _editId = w.Id;
        _editTokenMasked = w.BotTokenMasked;
        _form = new SaveWebhookRequest
        {
            Name = w.Name,
            Type = w.Type,
            Url = w.Url,
            ChatId = w.ChatId,
            BotToken = null,
            IsEnabled = w.IsEnabled,
            OnDryerFinished = w.OnDryerFinished,
            OnSpoolLow = w.OnSpoolLow,
            OnSpoolConsumed = w.OnSpoolConsumed
        };
        _error = null;
    }

    private void Cancel()
    {
        if (!_saving)
            CloseForm();
    }

    private void CloseForm()
    {
        _form = null;
        _editId = null;
        _editTokenMasked = null;
        _error = null;
    }

    private async Task SaveAsync()
    {
        if (_form == null)
            return;
        _saving = true;
        _error = null;
        try
        {
            var resp = _editId.HasValue
                ? await Webhooks.UpdateAsync(_editId.Value, _form)
                : await Webhooks.CreateAsync(_form);
            if (resp.IsSuccessStatusCode)
            {
                CloseForm();
                await LoadAsync();
            }
            else
                _error = await ReadErrorAsync(resp);
        }
        finally
        {
            _saving = false;
        }
    }

    private void AskDelete(WebhookDto w)
    {
        _deleteError = null;
        _deleteTarget = w;
    }

    private async Task DeleteAsync()
    {
        if (_deleteTarget == null)
            return;
        _deleting = true;
        try
        {
            var resp = await Webhooks.DeleteAsync(_deleteTarget.Id);
            if (!resp.IsSuccessStatusCode)
            {
                _deleteError = await ReadErrorAsync(resp);
                return;
            }
            var name = _deleteTarget.Name;
            _deleteTarget = null;
            _deleteError = null;
            await LoadAsync();
            ShowNotice(L.Format("webhook.deleted", name), true);
        }
        finally
        {
            _deleting = false;
        }
    }

    private async Task ToggleEnabledAsync(WebhookDto w)
    {
        _togglingId = w.Id;
        try
        {
            var request = new SaveWebhookRequest
            {
                Name = w.Name,
                Type = w.Type,
                Url = w.Url,
                ChatId = w.ChatId,
                BotToken = null,
                IsEnabled = !w.IsEnabled,
                OnDryerFinished = w.OnDryerFinished,
                OnSpoolLow = w.OnSpoolLow,
                OnSpoolConsumed = w.OnSpoolConsumed
            };
            var resp = await Webhooks.UpdateAsync(w.Id, request);
            if (resp.IsSuccessStatusCode)
            {
                await LoadAsync();
                ShowNotice(L.Format(request.IsEnabled ? "webhook.resumed" : "webhook.paused.notice", w.Name), true);
            }
            else
                ShowNotice(await ReadErrorAsync(resp), false);
        }
        finally
        {
            _togglingId = null;
        }
    }

    private void ShowNotice(string text, bool success)
    {
        _notice = text;
        _noticeSuccess = success;
    }

    private async Task TestAsync(WebhookDto w)
    {
        _testingId = w.Id;
        _testTargetId = w.Id;
        _testMessage = null;
        try
        {
            var result = await Webhooks.TestAsync(w.Id);
            _testSuccess = result?.Success == true;
            _testMessage = _testSuccess
                ? L["webhook.test.success"]
                : $"{L["webhook.test.failed"]}: {result?.Error ?? L["common.error"]}";
        }
        catch (Exception ex)
        {
            _testSuccess = false;
            _testMessage = $"{L["webhook.test.failed"]}: {ex.Message}";
        }
        finally
        {
            _testingId = null;
        }
        await LoadAsync();
    }

    private Task<string> ReadErrorAsync(HttpResponseMessage resp) => ResponseMessages.ReadAsync(resp, L["common.error"]);

    private string TypeLabel(string type) => L[$"webhook.type.{type}"];

    private static string TypeIcon(string type) => type switch
    {
        "discord" => "bi-discord",
        "telegram" => "bi-telegram",
        "homeassistant" => "bi-house-gear",
        _ => "bi-braces"
    };

    private static string TargetLabel(WebhookDto w)
    {
        if (w.Type == "telegram")
            return $"chat {w.ChatId} · {w.BotTokenMasked}";
        if (Uri.TryCreate(w.Url, UriKind.Absolute, out var uri))
            return $"{uri.Scheme}://{uri.Authority}{(uri.AbsolutePath.Length > 1 ? "/…" : "")}";
        return w.Url ?? "";
    }

    private static string UrlPlaceholder(string type) => type switch
    {
        "discord" => "https://discord.com/api/webhooks/…",
        "homeassistant" => "http://homeassistant.local:8123/api/webhook/spoolhero",
        _ => "https://example.com/hooks/spoolhero"
    };

    private string UrlHint(string type) => L[$"webhook.url.hint.{type}"];

    private static string FormatTime(DateTime? dt) =>
        dt == null ? "" : DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc).ToLocalTime().ToString("g");
}
