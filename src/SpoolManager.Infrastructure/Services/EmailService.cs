using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Infrastructure.Repositories;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Services;

public interface IEmailService
{
    Task<bool> IsEnabledAsync();
    Task<SmtpSettings?> GetSettingsAsync();
    Task SaveSettingsAsync(SmtpSettings settings);
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody);
    Task SendVerificationEmailAsync(AppUser user);
    Task SendPasswordResetEmailAsync(AppUser user);
    Task NotifyAdminsNewTicketAsync(SupportTicket ticket, IEnumerable<AppUser> admins);
    Task NotifyTicketReplyAsync(SupportTicket ticket, AppUser recipient, string replyContent, bool replyIsFromAdmin);
    Task NotifyDryerDoneAsync(Dryer dryer, IEnumerable<AppUser> recipients);
    Task NotifySpoolLowAsync(Spool spool, IEnumerable<AppUser> recipients);
}

public class EmailService : IEmailService
{
    private readonly ISmtpSettingsRepository _repo;

    public EmailService(ISmtpSettingsRepository repo) => _repo = repo;

    public async Task<bool> IsEnabledAsync()
    {
        var s = await _repo.GetAsync();
        return s is { IsEnabled: true } && !string.IsNullOrWhiteSpace(s.Host);
    }

    public Task<SmtpSettings?> GetSettingsAsync() => _repo.GetAsync();

    public async Task SaveSettingsAsync(SmtpSettings settings) =>
        await _repo.SaveAsync(settings);

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var cfg = await _repo.GetAsync();
        if (cfg == null || !cfg.IsEnabled || string.IsNullOrWhiteSpace(cfg.Host))
            return false;

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(cfg.FromName, cfg.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            var socketOptions = cfg.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : cfg.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
            await client.ConnectAsync(cfg.Host, cfg.Port, socketOptions);
            if (!string.IsNullOrWhiteSpace(cfg.Username))
                await client.AuthenticateAsync(cfg.Username, cfg.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return true;
        }
        catch { return false; }
    }

    public async Task SendVerificationEmailAsync(AppUser user)
    {
        var cfg = await _repo.GetAsync();
        if (cfg == null || !cfg.IsEnabled)
            return;
        var url = $"{cfg.BaseUrl.TrimEnd('/')}/verify-email?token={user.EmailVerificationToken}";
        var lang = user.PreferredLanguage;
        var body = EmailTemplates.Verification(user.Username, url, lang);
        var subject = lang == "en"
            ? "Confirm your email address – SpoolHero"
            : "E-Mail-Adresse bestätigen – SpoolHero";
        await SendAsync(user.Email, user.Username, subject, body);
    }

    public async Task SendPasswordResetEmailAsync(AppUser user)
    {
        var cfg = await _repo.GetAsync();
        if (cfg == null || !cfg.IsEnabled)
            return;
        var url = $"{cfg.BaseUrl.TrimEnd('/')}/reset-password?token={user.PasswordResetToken}";
        var lang = user.PreferredLanguage;
        var body = EmailTemplates.PasswordReset(user.Username, url, lang);
        var subject = lang == "en"
            ? "Reset your password – SpoolHero"
            : "Passwort zuruecksetzen – SpoolHero";
        await SendAsync(user.Email, user.Username, subject, body);
    }

    public async Task NotifyAdminsNewTicketAsync(SupportTicket ticket, IEnumerable<AppUser> admins)
    {
        var cfg = await _repo.GetAsync();
        if (cfg == null || !cfg.IsEnabled)
            return;
        var url = $"{cfg.BaseUrl.TrimEnd('/')}/admin/tickets/{ticket.Id}";
        foreach (var admin in admins)
        {
            var lang = admin.PreferredLanguage;
            var body = EmailTemplates.NewTicket(ticket.Subject, ticket.Username, ticket.Description, url, lang);
            var subject = lang == "en"
                ? $"New Support Ticket: {ticket.Subject}"
                : $"Neues Support-Ticket: {ticket.Subject}";
            await SendAsync(admin.Email, admin.Username, subject, body);
        }
    }

    public async Task NotifyTicketReplyAsync(SupportTicket ticket, AppUser recipient, string replyContent, bool replyIsFromAdmin)
    {
        if (!recipient.NotifyTicketReply)
            return;
        var cfg = await _repo.GetAsync();
        if (cfg == null || !cfg.IsEnabled)
            return;
        var url = $"{cfg.BaseUrl.TrimEnd('/')}/tickets/{ticket.Id}";
        var lang = recipient.PreferredLanguage;
        var body = EmailTemplates.TicketReply(ticket.Subject, recipient.Username, replyContent, replyIsFromAdmin, url, lang);
        var subject = lang == "en"
            ? $"New reply to your ticket: {ticket.Subject}"
            : $"Neue Antwort auf Ihr Ticket: {ticket.Subject}";
        await SendAsync(recipient.Email, recipient.Username, subject, body);
    }

    public async Task NotifyDryerDoneAsync(Dryer dryer, IEnumerable<AppUser> recipients)
    {
        var cfg = await _repo.GetAsync();
        if (cfg == null || !cfg.IsEnabled)
            return;
        foreach (var u in recipients.Where(r => r.NotifyDryerDone))
        {
            var lang = u.PreferredLanguage;
            var body = EmailTemplates.DryerDone(dryer.Name, lang);
            var subject = lang == "en"
                ? $"Drying complete: {dryer.Name}"
                : $"Trocknung abgeschlossen: {dryer.Name}";
            await SendAsync(u.Email, u.Username, subject, body);
        }
    }

    public async Task NotifySpoolLowAsync(Spool spool, IEnumerable<AppUser> recipients)
    {
        var cfg = await _repo.GetAsync();
        if (cfg == null || !cfg.IsEnabled)
            return;
        var materialName = spool.FilamentMaterial != null
            ? $"{spool.FilamentMaterial.Brand} {spool.FilamentMaterial.Type}"
            : "Unknown";
        foreach (var u in recipients.Where(r => r.NotifySpoolLow))
        {
            var lang = u.PreferredLanguage;
            var body = EmailTemplates.SpoolLow(materialName, (double)spool.RemainingPercent, lang);
            var subject = lang == "en"
                ? $"Spool running low: {materialName}"
                : $"Spule fast leer: {materialName}";
            await SendAsync(u.Email, u.Username, subject, body);
        }
    }
}
