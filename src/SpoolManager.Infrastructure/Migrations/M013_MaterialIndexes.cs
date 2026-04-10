using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(13)]
public class M013_MaterialIndexes : Migration
{
    public override void Up()
    {
        Create.Index("IX_filament_materials_project_id")
            .OnTable("filament_materials").OnColumn("project_id");

        Create.Index("IX_filament_materials_type")
            .OnTable("filament_materials").OnColumn("type");

        Create.Index("IX_filament_materials_brand")
            .OnTable("filament_materials").OnColumn("brand");

        Create.Index("IX_filament_materials_ofd_variant_id")
            .OnTable("filament_materials").OnColumn("ofd_variant_id");

        Create.Index("IX_material_suggestions_status")
            .OnTable("material_suggestions").OnColumn("status");

        Create.Index("IX_material_suggestions_user_id")
            .OnTable("material_suggestions").OnColumn("user_id");
    }

    public override void Down()
    {
        Delete.Index("IX_filament_materials_project_id").OnTable("filament_materials");
        Delete.Index("IX_filament_materials_type").OnTable("filament_materials");
        Delete.Index("IX_filament_materials_brand").OnTable("filament_materials");
        Delete.Index("IX_filament_materials_ofd_variant_id").OnTable("filament_materials");
        Delete.Index("IX_material_suggestions_status").OnTable("material_suggestions");
        Delete.Index("IX_material_suggestions_user_id").OnTable("material_suggestions");
    }
}
