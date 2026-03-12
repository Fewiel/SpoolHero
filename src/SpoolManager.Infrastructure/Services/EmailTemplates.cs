namespace SpoolManager.Infrastructure.Services;

public static class EmailTemplates
{
    private static string Wrap(string title, string content, string lang) => $"""
        <!DOCTYPE html><html><head><meta charset="utf-8"></head>
        <body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:20px">
        <div style="max-width:600px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.1)">
          <div style="background:#0d6efd;padding:20px 30px">
            <h1 style="color:#fff;margin:0;font-size:20px">🧵 SpoolHero</h1>
          </div>
          <div style="padding:30px">
            <h2 style="margin-top:0;color:#212529">{title}</h2>
            {content}
          </div>
          <div style="background:#f8f9fa;padding:15px 30px;border-top:1px solid #dee2e6;font-size:12px;color:#6c757d">
            SpoolHero – {(lang == "en" ? "Filament Management for 3D Printing" : "Filament-Verwaltung für 3D-Druck")}
          </div>
        </div>
        </body></html>
        """;

    private static string TicketFooterDe => """
        <hr style="border:none;border-top:1px solid #dee2e6;margin:20px 0">
        <p style="font-size:12px;color:#6c757d">
          ⚠️ <strong>Bitte nicht auf diese E-Mail antworten.</strong>
          Nutze die SpoolHero-Weboberfläche, um auf dein Support-Ticket zu antworten.
        </p>
        """;

    private static string TicketFooterEn => """
        <hr style="border:none;border-top:1px solid #dee2e6;margin:20px 0">
        <p style="font-size:12px;color:#6c757d">
          ⚠️ <strong>Please do not reply to this email.</strong>
          To respond to your support ticket, please use the SpoolHero web interface.
        </p>
        """;

    public static string Verification(string username, string verificationUrl, string lang = "en") =>
        lang == "en"
            ? Wrap("Confirm your email address",
                $"""
                <p>Hello <strong>{username}</strong>,</p>
                <p>please confirm your email address by clicking the link below:</p>
                <p style="margin:25px 0;text-align:center">
                  <a href="{verificationUrl}"
                     style="background:#0d6efd;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                    Confirm Email
                  </a>
                </p>
                <p style="color:#6c757d;font-size:13px">The link is valid for 24 hours.<br>
                If you did not register, you can safely ignore this email.</p>
                """, "en")
            : Wrap("E-Mail-Adresse bestätigen",
                $"""
                <p>Hallo <strong>{username}</strong>,</p>
                <p>bitte bestätige deine E-Mail-Adresse, indem du auf den folgenden Link klickst:</p>
                <p style="margin:25px 0;text-align:center">
                  <a href="{verificationUrl}"
                     style="background:#0d6efd;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                    E-Mail bestätigen
                  </a>
                </p>
                <p style="color:#6c757d;font-size:13px">Der Link ist 24 Stunden gültig.<br>
                Falls du dich nicht registriert hast, kannst du diese E-Mail ignorieren.</p>
                """, "de");

    public static string NewTicket(string subject, string username, string description, string ticketUrl, string lang = "en") =>
        lang == "en"
            ? Wrap("New Support Ticket",
                $"""
                <p>A new support ticket has been submitted.</p>
                <table style="width:100%;border-collapse:collapse;margin:15px 0">
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold;width:120px">Subject</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{subject}</td></tr>
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold">Created by</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{username}</td></tr>
                </table>
                <div style="background:#f8f9fa;padding:12px;border-left:4px solid #0d6efd;margin:15px 0;font-size:13px">
                  {description[..Math.Min(description.Length, 300)]}{(description.Length > 300 ? "…" : "")}
                </div>
                <p style="text-align:center;margin:25px 0">
                  <a href="{ticketUrl}" style="background:#0d6efd;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold">Open Ticket</a>
                </p>
                {TicketFooterEn}
                """, "en")
            : Wrap("Neues Support-Ticket",
                $"""
                <p>Ein neues Support-Ticket wurde eingereicht.</p>
                <table style="width:100%;border-collapse:collapse;margin:15px 0">
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold;width:120px">Betreff</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{subject}</td></tr>
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold">Erstellt von</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{username}</td></tr>
                </table>
                <div style="background:#f8f9fa;padding:12px;border-left:4px solid #0d6efd;margin:15px 0;font-size:13px">
                  {description[..Math.Min(description.Length, 300)]}{(description.Length > 300 ? "…" : "")}
                </div>
                <p style="text-align:center;margin:25px 0">
                  <a href="{ticketUrl}" style="background:#0d6efd;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold">Ticket öffnen</a>
                </p>
                {TicketFooterDe}
                """, "de");

    public static string TicketReply(string subject, string recipientName, string replyContent, bool fromAdmin, string ticketUrl, string lang = "en") =>
        lang == "en"
            ? Wrap("New Reply to Your Ticket",
                $"""
                <p>Hello <strong>{recipientName}</strong>,</p>
                <p>there is a new reply{(fromAdmin ? " from the support team" : "")} on your ticket <strong>"{subject}"</strong>.</p>
                <div style="background:#f8f9fa;padding:12px;border-left:4px solid #198754;margin:15px 0;font-size:13px">
                  {replyContent[..Math.Min(replyContent.Length, 500)]}{(replyContent.Length > 500 ? "…" : "")}
                </div>
                <p style="text-align:center;margin:25px 0">
                  <a href="{ticketUrl}" style="background:#0d6efd;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold">View Ticket</a>
                </p>
                {TicketFooterEn}
                """, "en")
            : Wrap("Neue Antwort auf Ihr Ticket",
                $"""
                <p>Hallo <strong>{recipientName}</strong>,</p>
                <p>auf Ihr Support-Ticket <strong>„{subject}"</strong> gibt es eine neue Antwort{(fromAdmin ? " vom Support-Team" : "")}.</p>
                <div style="background:#f8f9fa;padding:12px;border-left:4px solid #198754;margin:15px 0;font-size:13px">
                  {replyContent[..Math.Min(replyContent.Length, 500)]}{(replyContent.Length > 500 ? "…" : "")}
                </div>
                <p style="text-align:center;margin:25px 0">
                  <a href="{ticketUrl}" style="background:#0d6efd;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;font-weight:bold">Ticket anzeigen</a>
                </p>
                {TicketFooterDe}
                """, "de");

    public static string DryerDone(string dryerName, string lang = "en") =>
        lang == "en"
            ? Wrap("Drying Complete",
                $"""
                <p>The drying cycle in <strong>{dryerName}</strong> is finished.</p>
                <p>The filament can now be removed.</p>
                <p style="background:#fff3cd;padding:12px;border-radius:6px;font-size:13px">
                  ⚠️ Please remove the filament promptly to prevent moisture re-absorption.
                </p>
                """, "en")
            : Wrap("Trocknung abgeschlossen",
                $"""
                <p>Die Trocknung in <strong>{dryerName}</strong> ist abgeschlossen.</p>
                <p>Das Filament kann jetzt entnommen werden.</p>
                <p style="background:#fff3cd;padding:12px;border-radius:6px;font-size:13px">
                  ⚠️ Bitte entnimm das Filament zeitnah, um eine erneute Feuchtigkeitsaufnahme zu vermeiden.
                </p>
                """, "de");

    public static string SpoolLow(string materialName, double remainingPercent, string lang = "en") =>
        lang == "en"
            ? Wrap("Spool Running Low",
                $"""
                <p>A spool is almost empty and should be reordered soon.</p>
                <table style="width:100%;border-collapse:collapse;margin:15px 0">
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold;width:150px">Material</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{materialName}</td></tr>
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold">Remaining</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{remainingPercent:F0} %</td></tr>
                </table>
                <p>Log in to SpoolHero to view details and reorder if needed.</p>
                """, "en")
            : Wrap("Spule fast leer",
                $"""
                <p>Eine Spule ist fast leer und sollte bald nachbestellt werden.</p>
                <table style="width:100%;border-collapse:collapse;margin:15px 0">
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold;width:150px">Material</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{materialName}</td></tr>
                  <tr><td style="padding:8px;background:#f8f9fa;font-weight:bold">Restmenge</td><td style="padding:8px;border-bottom:1px solid #dee2e6">{remainingPercent:F0} %</td></tr>
                </table>
                <p>Melde dich bei SpoolHero an, um Details zu sehen und ggf. nachzubestellen.</p>
                """, "de");
}
