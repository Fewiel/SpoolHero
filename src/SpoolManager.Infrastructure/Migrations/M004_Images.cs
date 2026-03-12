using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(4)]
public class M004_Images : Migration
{
    public override void Up()
    {
        Alter.Table("printers")
            .AddColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .AddColumn("image_content_type").AsString(50).Nullable();

        Alter.Table("storage_locations")
            .AddColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .AddColumn("image_content_type").AsString(50).Nullable();

        Alter.Table("dryers")
            .AddColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .AddColumn("image_content_type").AsString(50).Nullable();
    }

    public override void Down()
    {
        Delete.Column("image_data").Column("image_content_type").FromTable("printers");
        Delete.Column("image_data").Column("image_content_type").FromTable("storage_locations");
        Delete.Column("image_data").Column("image_content_type").FromTable("dryers");
    }
}
