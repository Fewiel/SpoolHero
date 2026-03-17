using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(12)]
public class M012_MaterialSyncFields : Migration
{
    public override void Up()
    {
        Alter.Table("filament_materials")
            .AddColumn("source").AsString(20).NotNullable().WithDefaultValue("manual")
            .AddColumn("external_id").AsString(255).Nullable()
            .AddColumn("product_name").AsString(300).Nullable()
            .AddColumn("spool_weight_grams").AsInt32().Nullable()
            .AddColumn("spool_type").AsString(20).Nullable()
            .AddColumn("finish").AsString(20).Nullable()
            .AddColumn("translucent").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("glow").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("fill").AsString(20).Nullable();

        Create.Index("IX_filament_materials_external_id")
            .OnTable("filament_materials")
            .OnColumn("external_id")
            .Unique();

        Create.Index("IX_filament_materials_source")
            .OnTable("filament_materials")
            .OnColumn("source");

        Create.Index("IX_filament_materials_search")
            .OnTable("filament_materials")
            .OnColumn("brand").Ascending()
            .OnColumn("type").Ascending()
            .OnColumn("product_name").Ascending()
            .OnColumn("color_name").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_filament_materials_search").OnTable("filament_materials");
        Delete.Index("IX_filament_materials_source").OnTable("filament_materials");
        Delete.Index("IX_filament_materials_external_id").OnTable("filament_materials");

        Delete.Column("fill").FromTable("filament_materials");
        Delete.Column("glow").FromTable("filament_materials");
        Delete.Column("translucent").FromTable("filament_materials");
        Delete.Column("finish").FromTable("filament_materials");
        Delete.Column("spool_type").FromTable("filament_materials");
        Delete.Column("spool_weight_grams").FromTable("filament_materials");
        Delete.Column("product_name").FromTable("filament_materials");
        Delete.Column("external_id").FromTable("filament_materials");
        Delete.Column("source").FromTable("filament_materials");
    }
}
