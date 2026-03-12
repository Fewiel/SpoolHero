using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(10)]
public class M010_PreferredLanguage : Migration
{
    public override void Up()
    {
        Alter.Table("users").AddColumn("preferred_language").AsString(5).NotNullable().WithDefaultValue("de");
    }

    public override void Down()
    {
        Delete.Column("preferred_language").FromTable("users");
    }
}
