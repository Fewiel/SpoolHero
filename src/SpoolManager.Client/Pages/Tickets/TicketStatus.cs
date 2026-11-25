using SpoolManager.Client.Services;

namespace SpoolManager.Client.Pages.Tickets;

/// <summary>Shared texts, colours and icons for support ticket states.</summary>
internal static class TicketStatus
{
    public static string Label(LocalizationService l, string status) => status switch
    {
        "in_progress" => l["ticket.status.in_progress"],
        "answered" => l["ticket.status.answered"],
        "closed" => l["ticket.status.closed"],
        _ => l["ticket.status.open"]
    };

    /// <summary>What the status means for the person who opened the ticket.</summary>
    public static string Explanation(LocalizationService l, string status) => status switch
    {
        "in_progress" => l["ticket.status.in_progress.text"],
        "answered" => l["ticket.status.answered.text"],
        "closed" => l["ticket.status.closed.text"],
        _ => l["ticket.status.open.text"]
    };

    public static string ChipClass(string status) => status switch
    {
        "in_progress" => "sh-chip-accent",
        "answered" => "sh-chip-ok",
        "closed" => "",
        _ => "sh-chip-warn"
    };

    public static string Icon(string status) => status switch
    {
        "in_progress" => "bi-hourglass-split",
        "answered" => "bi-chat-left-text",
        "closed" => "bi-check2-circle",
        _ => "bi-envelope-open"
    };

    public static DateTime AsLocal(DateTime dt) =>
        (dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt).ToLocalTime();

    public static string FormatDate(DateTime dt) => AsLocal(dt).ToString("d");

    public static string FormatDateTime(DateTime dt) => AsLocal(dt).ToString("g");
}
