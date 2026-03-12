using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(2)]
public class M002_AuditLog : Migration
{
    public override void Up()
    {
        Create.Table("audit_logs")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("timestamp").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("user_id").AsInt32().Nullable()
            .WithColumn("username").AsString(100).Nullable()
            .WithColumn("action").AsString(100).NotNullable()
            .WithColumn("entity_type").AsString(50).Nullable()
            .WithColumn("entity_id").AsInt32().Nullable()
            .WithColumn("entity_name").AsString(200).Nullable()
            .WithColumn("project_id").AsInt32().Nullable()
            .WithColumn("project_name").AsString(200).Nullable()
            .WithColumn("details").AsString(2000).Nullable()
            .WithColumn("ip_address").AsString(50).Nullable();

        Create.Index("idx_audit_timestamp").OnTable("audit_logs").OnColumn("timestamp").Descending();
        Create.Index("idx_audit_action").OnTable("audit_logs").OnColumn("action");
        Create.Index("idx_audit_user").OnTable("audit_logs").OnColumn("user_id");
    }

    public override void Down()
    {
        Delete.Table("audit_logs");
    }
}
