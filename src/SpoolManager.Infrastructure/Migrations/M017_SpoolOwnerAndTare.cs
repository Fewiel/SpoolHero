using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(17)]
public class M017_SpoolOwnerAndTare : Migration
{
    public override void Up()
    {
        Alter.Table("spools")
            .AddColumn("owner_name").AsString(100).Nullable()
            .AddColumn("empty_spool_weight_grams").AsDecimal(8, 1).Nullable();

        Alter.Table("filament_materials")
            .AddColumn("spool_weight_grams").AsInt32().Nullable();

        Create.Index("ix_spools_project_owner")
            .OnTable("spools")
            .OnColumn("project_id").Ascending()
            .OnColumn("owner_name").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_spools_project_owner").OnTable("spools");
        Delete.Column("owner_name").FromTable("spools");
        Delete.Column("empty_spool_weight_grams").FromTable("spools");
        Delete.Column("spool_weight_grams").FromTable("filament_materials");
    }
}
