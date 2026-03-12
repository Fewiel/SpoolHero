using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(6)]
public class M006_AdminFeatures : Migration
{
    public override void Up()
    {
        Alter.Table("users")
            .AddColumn("last_active_at").AsDateTime().Nullable();

        Alter.Table("filament_materials")
            .AddColumn("reorder_click_count").AsInt32().NotNullable().WithDefaultValue(0);

        Create.Table("support_tickets")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("user_id").AsString(36).NotNullable()
            .WithColumn("username").AsString(100).NotNullable()
            .WithColumn("subject").AsString(300).NotNullable()
            .WithColumn("description").AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn("status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("assigned_to_user_id").AsString(36).Nullable()
            .WithColumn("assigned_to_username").AsString(100).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("ticket_comments")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("ticket_id").AsString(36).NotNullable()
            .WithColumn("user_id").AsString(36).NotNullable()
            .WithColumn("username").AsString(100).NotNullable()
            .WithColumn("is_admin").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("content").AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("ix_tickets_user_id").OnTable("support_tickets").OnColumn("user_id");
        Create.Index("ix_tickets_status").OnTable("support_tickets").OnColumn("status");
        Create.Index("ix_ticket_comments_ticket_id").OnTable("ticket_comments").OnColumn("ticket_id");
    }

    public override void Down()
    {
        Delete.Table("ticket_comments");
        Delete.Table("support_tickets");
        Delete.Column("reorder_click_count").FromTable("filament_materials");
        Delete.Column("last_active_at").FromTable("users");
    }
}
