using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(13)]
public class M013_SlicerProfiles : Migration
{
    public override void Up()
    {
        Create.Table("slicer_profiles")
            .WithColumn("id").AsCustom("CHAR(36)").PrimaryKey()
            .WithColumn("filament_material_id").AsCustom("CHAR(36)").NotNullable()
            .WithColumn("project_id").AsCustom("CHAR(36)").Nullable()
            .WithColumn("printer_id").AsCustom("CHAR(36)").Nullable()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("slicer_type").AsString(20).NotNullable().WithDefaultValue("generic")
            .WithColumn("nozzle_temp").AsInt32().Nullable()
            .WithColumn("nozzle_temp_initial_layer").AsInt32().Nullable()
            .WithColumn("bed_temp").AsInt32().Nullable()
            .WithColumn("bed_temp_initial_layer").AsInt32().Nullable()
            .WithColumn("chamber_temp").AsInt32().Nullable()
            .WithColumn("max_volumetric_speed").AsDecimal(6, 2).Nullable()
            .WithColumn("filament_flow_ratio").AsDecimal(4, 3).Nullable()
            .WithColumn("pressure_advance").AsDecimal(6, 4).Nullable()
            .WithColumn("retraction_length").AsDecimal(4, 2).Nullable()
            .WithColumn("retraction_speed").AsInt32().Nullable()
            .WithColumn("z_hop").AsDecimal(4, 2).Nullable()
            .WithColumn("fan_min_speed").AsInt32().Nullable()
            .WithColumn("fan_max_speed").AsInt32().Nullable()
            .WithColumn("fan_disable_first_layers").AsInt32().Nullable()
            .WithColumn("overhang_fan_speed").AsInt32().Nullable()
            .WithColumn("filament_start_gcode").AsCustom("TEXT").Nullable()
            .WithColumn("filament_end_gcode").AsCustom("TEXT").Nullable()
            .WithColumn("notes").AsCustom("TEXT").Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.ForeignKey("FK_slicer_profiles_material")
            .FromTable("slicer_profiles").ForeignColumn("filament_material_id")
            .ToTable("filament_materials").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("FK_slicer_profiles_project")
            .FromTable("slicer_profiles").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("FK_slicer_profiles_printer")
            .FromTable("slicer_profiles").ForeignColumn("printer_id")
            .ToTable("printers").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.Index("IX_slicer_profiles_material_slicer")
            .OnTable("slicer_profiles")
            .OnColumn("filament_material_id").Ascending()
            .OnColumn("slicer_type").Ascending();

        Create.Index("IX_slicer_profiles_printer")
            .OnTable("slicer_profiles")
            .OnColumn("printer_id");
    }

    public override void Down()
    {
        Delete.Table("slicer_profiles");
    }
}
