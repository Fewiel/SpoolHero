using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(9)]
public class M009_SiteSettings : Migration
{
    public override void Up()
    {
        Create.Table("site_settings")
            .WithColumn("key").AsString(100).PrimaryKey()
            .WithColumn("value").AsCustom("MEDIUMTEXT").Nullable()
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
    }

    public override void Down()
    {
        Delete.Table("site_settings");
    }
}
