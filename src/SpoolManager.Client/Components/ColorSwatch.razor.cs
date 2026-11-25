using Microsoft.AspNetCore.Components;

namespace SpoolManager.Client.Components;

/// <summary>Small filament colour dot (design system class <c>sh-color-swatch</c>).</summary>
public partial class ColorSwatch
{
    /// <summary>Colour as hex, with or without leading '#'.</summary>
    [Parameter] public string? ColorHex { get; set; }

    /// <summary>Accessible name (e.g. the colour name). Without it the swatch is decorative.</summary>
    [Parameter] public string? Label { get; set; }

    [Parameter] public string? Class { get; set; }

    private string Hex => (ColorHex ?? string.Empty).Trim().TrimStart('#');

    private string? ColorCss => Hex.Length is 3 or 6 or 8 && Hex.All(Uri.IsHexDigit) ? "#" + Hex : null;
}
