using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(8)]
public class M008_MailAndUserFeatures : Migration
{
    public override void Up()
    {
        // Users: super admin + email verification + notification prefs
        Alter.Table("users").AddColumn("is_super_admin").AsBoolean().NotNullable().WithDefaultValue(false);
        Alter.Table("users").AddColumn("email_verified").AsBoolean().NotNullable().WithDefaultValue(true);
        Alter.Table("users").AddColumn("email_verification_token").AsString(128).Nullable();
        Alter.Table("users").AddColumn("email_verification_token_expires").AsDateTime().Nullable();
        Alter.Table("users").AddColumn("notify_spool_low").AsBoolean().NotNullable().WithDefaultValue(true);
        Alter.Table("users").AddColumn("notify_dryer_done").AsBoolean().NotNullable().WithDefaultValue(true);
        Alter.Table("users").AddColumn("notify_ticket_reply").AsBoolean().NotNullable().WithDefaultValue(true);

        // Dryers: drying state for notifications
        Alter.Table("dryers").AddColumn("is_drying").AsBoolean().NotNullable().WithDefaultValue(false);
        Alter.Table("dryers").AddColumn("drying_started_at").AsDateTime().Nullable();
        Alter.Table("dryers").AddColumn("drying_finish_at").AsDateTime().Nullable();
        Alter.Table("dryers").AddColumn("drying_notified_at").AsDateTime().Nullable();

        // Spools: low-spool notification tracking
        Alter.Table("spools").AddColumn("low_spool_notified_at").AsDateTime().Nullable();

        // SMTP settings (single row)
        Create.Table("smtp_settings")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("host").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("port").AsInt32().NotNullable().WithDefaultValue(587)
            .WithColumn("use_ssl").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("use_start_tls").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("username").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("password").AsString(1000).NotNullable().WithDefaultValue("")
            .WithColumn("from_email").AsString(255).NotNullable().WithDefaultValue("")
            .WithColumn("from_name").AsString(255).NotNullable().WithDefaultValue("SpoolManager")
            .WithColumn("is_enabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("base_url").AsString(500).NotNullable().WithDefaultValue("");
    }

    public override void Down()
    {
        Delete.Table("smtp_settings");
        Delete.Column("low_spool_notified_at").FromTable("spools");
        Delete.Column("drying_notified_at").FromTable("dryers");
        Delete.Column("drying_finish_at").FromTable("dryers");
        Delete.Column("drying_started_at").FromTable("dryers");
        Delete.Column("is_drying").FromTable("dryers");
        Delete.Column("notify_ticket_reply").FromTable("users");
        Delete.Column("notify_dryer_done").FromTable("users");
        Delete.Column("notify_spool_low").FromTable("users");
        Delete.Column("email_verification_token_expires").FromTable("users");
        Delete.Column("email_verification_token").FromTable("users");
        Delete.Column("email_verified").FromTable("users");
        Delete.Column("is_super_admin").FromTable("users");
    }
}
