using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(3)]
public class M003_TokenVersionCascadeSeed : Migration
{
    public override void Up()
    {
        Alter.Table("users").AddColumn("token_version").AsInt32().NotNullable().WithDefaultValue(0);

        // Spools: nullable FKs → SET NULL (allow independent delete of printer/storage/dryer)
        Delete.ForeignKey("fk_spools_printer").OnTable("spools");
        Delete.ForeignKey("fk_spools_storage").OnTable("spools");
        Delete.ForeignKey("fk_spools_dryer").OnTable("spools");

        Create.ForeignKey("fk_spools_printer")
            .FromTable("spools").ForeignColumn("printer_id")
            .ToTable("printers").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("fk_spools_storage")
            .FromTable("spools").ForeignColumn("storage_location_id")
            .ToTable("storage_locations").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("fk_spools_dryer")
            .FromTable("spools").ForeignColumn("dryer_id")
            .ToTable("dryers").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.SetNull);

        // Project-owned tables → CASCADE on project delete
        Delete.ForeignKey("fk_spools_project").OnTable("spools");
        Create.ForeignKey("fk_spools_project")
            .FromTable("spools").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Delete.ForeignKey("fk_project_members_project").OnTable("project_members");
        Create.ForeignKey("fk_project_members_project")
            .FromTable("project_members").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Delete.ForeignKey("fk_invitations_project").OnTable("invitations");
        Create.ForeignKey("fk_invitations_project")
            .FromTable("invitations").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Delete.ForeignKey("fk_materials_project").OnTable("filament_materials");
        Create.ForeignKey("fk_materials_project")
            .FromTable("filament_materials").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Delete.ForeignKey("fk_printers_project").OnTable("printers");
        Create.ForeignKey("fk_printers_project")
            .FromTable("printers").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Delete.ForeignKey("fk_storage_project").OnTable("storage_locations");
        Create.ForeignKey("fk_storage_project")
            .FromTable("storage_locations").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);

        Delete.ForeignKey("fk_dryers_project").OnTable("dryers");
        Create.ForeignKey("fk_dryers_project")
            .FromTable("dryers").ForeignColumn("project_id")
            .ToTable("projects").PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down()
    {
        Delete.Column("token_version").FromTable("users");
    }
}
