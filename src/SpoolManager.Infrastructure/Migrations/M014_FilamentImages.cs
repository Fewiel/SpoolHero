using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(14)]
public class M014_FilamentImages : Migration
{
    public override void Up()
    {
        Alter.Table("filament_materials")
            .AddColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .AddColumn("image_content_type").AsString(50).Nullable();

        Alter.Table("spools")
            .AddColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .AddColumn("image_content_type").AsString(50).Nullable();
    }

    public override void Down()
    {
        Delete.Column("image_data").FromTable("filament_materials");
        Delete.Column("image_content_type").FromTable("filament_materials");
        Delete.Column("image_data").FromTable("spools");
        Delete.Column("image_content_type").FromTable("spools");
    }
}
