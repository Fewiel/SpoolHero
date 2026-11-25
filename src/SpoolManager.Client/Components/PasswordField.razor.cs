using Microsoft.AspNetCore.Components;
using SpoolManager.Client.Services;

namespace SpoolManager.Client.Components;

/// <summary>Password input with a button to show or hide the typed text.</summary>
public partial class PasswordField
{
    [Inject] private LocalizationService L { get; set; } = default!;

    [Parameter, EditorRequired] public string Id { get; set; } = string.Empty;
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string Autocomplete { get; set; } = "current-password";
    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public bool Invalid { get; set; }
    [Parameter] public string? DescribedBy { get; set; }

    /// <summary>Optional content after the input (e.g. an invalid-feedback element).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool _visible;

    private void Toggle() => _visible = !_visible;

    private Task OnInput(ChangeEventArgs e)
    {
        Value = e.Value?.ToString() ?? string.Empty;
        return ValueChanged.InvokeAsync(Value);
    }
}
