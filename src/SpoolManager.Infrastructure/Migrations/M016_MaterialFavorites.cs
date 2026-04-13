using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(16)]
public class M016_MaterialFavorites : Migration
{
    public override void Up()
    {
        Create.Table("user_material_favorites")
            .WithColumn("user_id").AsString(36).NotNullable()
            .WithColumn("material_id").AsString(36).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.PrimaryKey("pk_user_material_favorites")
            .OnTable("user_material_favorites")
            .Columns("user_id", "material_id");
    }

    public override void Down()
    {
        Delete.Table("user_material_favorites");
    }
}
