namespace SpoolManager.Shared.DTOs.Admin;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsPlatformAdmin { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

public class AdminStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsersLast15Min { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int TotalProjects { get; set; }
    public int ProjectsWithSpools { get; set; }
    public int TotalSpools { get; set; }
    public int TotalGlobalMaterials { get; set; }
    public int TotalReorderClicks { get; set; }
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public List<MaterialClickDto> TopClickedMaterials { get; set; } = [];
}

public class MaterialClickDto
{
    public Guid Id { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "FFFFFF";
    public int ClickCount { get; set; }
}

public class SetAdminRequest
{
    public bool IsAdmin { get; set; }
}

public class SetLandingPageRequest
{
    public bool Enabled { get; set; }
}

public class SmtpSettingsDto
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "SpoolManager";
    public bool IsEnabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
}

public class TestSmtpRequest
{
    public string ToEmail { get; set; } = string.Empty;
}

public class NotificationPrefsDto
{
    public bool NotifySpoolLow { get; set; }
    public bool NotifyDryerDone { get; set; }
    public bool NotifyTicketReply { get; set; }
}

public class LegalSettingsDto
{
    public string PrivacyDe { get; set; } = string.Empty;
    public string PrivacyEn { get; set; } = string.Empty;
    public string ImprintDe { get; set; } = string.Empty;
    public string ImprintEn { get; set; } = string.Empty;
    public string TermsDe { get; set; } = string.Empty;
    public string TermsEn { get; set; } = string.Empty;
}

public class BrandingDto
{
    public string? LogoDataUrl { get; set; }
    public string? LogoDarkDataUrl { get; set; }
    public string? FaviconDataUrl { get; set; }
    public bool LandingPageEnabled { get; set; } = true;
}
