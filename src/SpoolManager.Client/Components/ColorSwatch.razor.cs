using Microsoft.AspNetCore.Components;

namespace SpoolManager.Client.Components;

public partial class ColorSwatch
{
    [Parameter] public string? ColorHex { get; set; }
}
