using LinqToDB;
using LinqToDB.Data;
using SpoolManager.Shared.Models;

namespace SpoolManager.Infrastructure.Data;

public class SpoolManagerDb : DataConnection
{
    public SpoolManagerDb(DataOptions<SpoolManagerDb> options) : base(options.Options) { }

    public ITable<FilamentMaterial> FilamentMaterials => this.GetTable<FilamentMaterial>();
    public ITable<Spool> Spools => this.GetTable<Spool>();
    public ITable<Printer> Printers => this.GetTable<Printer>();
    public ITable<StorageLocation> StorageLocations => this.GetTable<StorageLocation>();
    public ITable<Dryer> Dryers => this.GetTable<Dryer>();
    public ITable<Project> Projects => this.GetTable<Project>();
    public ITable<ProjectMember> ProjectMembers => this.GetTable<ProjectMember>();
    public ITable<Invitation> Invitations => this.GetTable<Invitation>();
    public ITable<AppUser> Users => this.GetTable<AppUser>();
    public ITable<AuditLog> AuditLogs => this.GetTable<AuditLog>();
    public ITable<SupportTicket> Tickets => this.GetTable<SupportTicket>();
    public ITable<TicketComment> TicketComments => this.GetTable<TicketComment>();
    public ITable<SmtpSettings> SmtpSettings => this.GetTable<SmtpSettings>();
    public ITable<SiteSetting> SiteSettings => this.GetTable<SiteSetting>();
    public ITable<MaterialSuggestion> MaterialSuggestions => this.GetTable<MaterialSuggestion>();
}
