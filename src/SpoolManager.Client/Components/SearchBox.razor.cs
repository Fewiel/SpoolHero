using Microsoft.AspNetCore.Components;

namespace SpoolManager.Client.Components;

/// <summary>Search field with a magnifier icon for list toolbars; reports every keystroke.</summary>
public partial class SearchBox
{
    [Parameter, EditorRequired] public string Id { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string? Class { get; set; }

    private Task OnInput(ChangeEventArgs e)
    {
        Value = e.Value?.ToString() ?? string.Empty;
        return ValueChanged.InvokeAsync(Value);
    }
}
