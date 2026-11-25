using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace SpoolManager.Client.Components;

/// <summary>
/// A filament spool drawn as a ring in the filament's colour; the filled arc shows the remaining amount.
/// </summary>
public partial class SpoolRing
{
    private const double Circumference = 2 * Math.PI * 22;

    /// <summary>Filament colour as hex, with or without leading '#'.</summary>
    [Parameter] public string? ColorHex { get; set; }

    /// <summary>Remaining amount in percent (0–100).</summary>
    [Parameter] public decimal Percent { get; set; }

    /// <summary>Rendered size in px.</summary>
    [Parameter] public int Size { get; set; } = 48;

    /// <summary>Text for screen readers. When empty, the ring is treated as decorative.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    [Parameter] public string? Class { get; set; }

    /// <summary>Optional content in the middle of the ring (e.g. the percentage).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool Decorative => string.IsNullOrWhiteSpace(AriaLabel);

    private double ClampedPercent => Math.Clamp((double)Percent, 0, 100);

    private string ColorCss
    {
        get
        {
            var hex = (ColorHex ?? string.Empty).Trim().TrimStart('#');
            return hex.Length is 3 or 6 or 8 && hex.All(Uri.IsHexDigit) ? "#" + hex : "var(--sh-muted)";
        }
    }

    private string DashArray =>
        string.Create(CultureInfo.InvariantCulture, $"{Circumference * ClampedPercent / 100:0.##} {Circumference:0.##}");

    // Keep outline lines ~1px regardless of size
    private string OutlineWidth =>
        (56.0 / Size).ToString("0.###", CultureInfo.InvariantCulture);

    private int LabelSize => Math.Max(11, Size / 5);
}
