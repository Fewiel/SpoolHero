using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(11)]
public class M011_SpoolmanCompat : Migration
{
    public override void Up()
    {
        Execute.Sql("ALTER TABLE spools ADD COLUMN spoolman_id INT NOT NULL AUTO_INCREMENT UNIQUE");

        Create.Table("spoolman_api_keys")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable().ForeignKey("projects", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("api_key").AsString(64).NotNullable().Unique()
            .WithColumn("name").AsString(100).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("last_used_at").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table("spoolman_api_keys");
        Execute.Sql("ALTER TABLE spools DROP COLUMN spoolman_id");
    }
}
