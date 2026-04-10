using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(12)]
public class M012_OfdAndSuggestions : Migration
{
    public override void Up()
    {
        Alter.Table("filament_materials")
            .AddColumn("ofd_filament_id").AsString(40).Nullable()
            .AddColumn("ofd_variant_id").AsString(40).Nullable();

        Create.Table("material_suggestions")
            .WithColumn("id").AsCustom("CHAR(36)").PrimaryKey()
            .WithColumn("material_id").AsCustom("CHAR(36)").Nullable()
            .WithColumn("user_id").AsCustom("CHAR(36)").NotNullable()
            .WithColumn("username").AsString(100).NotNullable()
            .WithColumn("type").AsString(50).NotNullable()
            .WithColumn("brand").AsString(100).NotNullable()
            .WithColumn("color_hex").AsString(10).NotNullable().WithDefaultValue("FFFFFF")
            .WithColumn("color_name").AsString(100).Nullable()
            .WithColumn("min_temp").AsInt32().NotNullable()
            .WithColumn("max_temp").AsInt32().NotNullable()
            .WithColumn("bed_temp").AsInt32().Nullable()
            .WithColumn("diameter_mm").AsDecimal(5, 2).NotNullable().WithDefaultValue(1.75)
            .WithColumn("density").AsDecimal(5, 3).Nullable()
            .WithColumn("dry_temp").AsInt32().Nullable()
            .WithColumn("dry_time_hours").AsInt32().Nullable()
            .WithColumn("notes").AsCustom("TEXT").Nullable()
            .WithColumn("reorder_url").AsString(500).Nullable()
            .WithColumn("price_per_kg").AsDecimal(10, 2).Nullable()
            .WithColumn("status").AsString(20).NotNullable().WithDefaultValue("pending")
            .WithColumn("admin_notes").AsCustom("TEXT").Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("reviewed_at").AsDateTime().Nullable()
            .WithColumn("reviewed_by").AsCustom("CHAR(36)").Nullable();
    }

    public override void Down()
    {
        Delete.Table("material_suggestions");
        Delete.Column("ofd_filament_id").FromTable("filament_materials");
        Delete.Column("ofd_variant_id").FromTable("filament_materials");
    }
}
