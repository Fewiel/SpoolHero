using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(12)]
public class M012_SpoolmanCallLogs : Migration
{
    public override void Up()
    {
        Create.Table("spoolman_call_logs")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("api_key_id").AsString(36).NotNullable()
                .ForeignKey("fk_call_logs_apikey", "spoolman_api_keys", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("called_at").AsDateTime().NotNullable()
            .WithColumn("method").AsString(10).NotNullable()
            .WithColumn("path").AsString(255).NotNullable()
            .WithColumn("status_code").AsInt32().NotNullable();

        Create.Index("idx_spoolman_call_logs")
            .OnTable("spoolman_call_logs")
            .OnColumn("api_key_id").Ascending()
            .OnColumn("called_at").Ascending();
    }

    public override void Down()
    {
        Delete.Table("spoolman_call_logs");
    }
}
