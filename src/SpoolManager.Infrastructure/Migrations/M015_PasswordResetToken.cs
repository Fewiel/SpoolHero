using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(15)]
public class M015_PasswordResetToken : Migration
{
    public override void Up()
    {
        Alter.Table("users")
            .AddColumn("password_reset_token").AsString(100).Nullable()
            .AddColumn("password_reset_token_expires").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("password_reset_token").FromTable("users");
        Delete.Column("password_reset_token_expires").FromTable("users");
    }
}
