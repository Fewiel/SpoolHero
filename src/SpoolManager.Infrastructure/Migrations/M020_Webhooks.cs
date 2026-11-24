using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(20)]
public class M020_Webhooks : Migration
{
    public override void Up()
    {
        Create.Table("webhooks")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable()
                .ForeignKey("fk_webhooks_project", "projects", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("type").AsString(30).NotNullable()
            .WithColumn("url").AsString(2000).Nullable()
            .WithColumn("bot_token").AsString(200).Nullable()
            .WithColumn("chat_id").AsString(100).Nullable()
            .WithColumn("is_enabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("on_dryer_finished").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("on_spool_low").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("on_spool_consumed").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("last_triggered_at").AsDateTime().Nullable()
            .WithColumn("last_success").AsBoolean().Nullable()
            .WithColumn("last_status_code").AsInt32().Nullable()
            .WithColumn("last_error").AsString(1000).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("ix_webhooks_project").OnTable("webhooks").OnColumn("project_id");
    }

    public override void Down()
    {
        Delete.Table("webhooks");
    }
}
