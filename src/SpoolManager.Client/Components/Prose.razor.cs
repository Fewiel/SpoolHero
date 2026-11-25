using Microsoft.AspNetCore.Components;

namespace SpoolManager.Client.Components;

/// <summary>Readable typography for HTML content (legal texts, changelog entries).</summary>
public partial class Prose
{
    /// <summary>Trusted HTML (admin-provided legal texts or rendered Markdown).</summary>
    [Parameter] public string? Html { get; set; }

    [Parameter] public string? Class { get; set; }
}
