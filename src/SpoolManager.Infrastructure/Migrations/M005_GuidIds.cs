using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(5)]
public class M005_GuidIds : Migration
{
    public override void Up()
    {
        Delete.Table("audit_logs");
        Delete.Table("invitations");
        Delete.Table("spools");
        Delete.Table("filament_materials");
        Delete.Table("project_members");
        Delete.Table("dryers");
        Delete.Table("storage_locations");
        Delete.Table("printers");
        Delete.Table("projects");
        Delete.Table("users");

        Create.Table("users")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("username").AsString(100).NotNullable().Unique()
            .WithColumn("email").AsString(256).NotNullable().Unique()
            .WithColumn("password_hash").AsString(256).NotNullable()
            .WithColumn("is_platform_admin").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("token_version").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("projects")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("description").AsString(2000).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("project_members")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable()
                .ForeignKey("fk_pm_project", "projects", "id")
            .WithColumn("user_id").AsString(36).NotNullable()
                .ForeignKey("fk_pm_user", "users", "id")
            .WithColumn("role").AsString(50).NotNullable().WithDefaultValue("member")
            .WithColumn("username").AsString(100).Nullable()
            .WithColumn("email").AsString(256).Nullable()
            .WithColumn("joined_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("printers")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable()
                .ForeignKey("fk_printers_project", "projects", "id")
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("notes").AsString(2000).Nullable()
            .WithColumn("rfid_tag_uid").AsString(256).Nullable()
            .WithColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .WithColumn("image_content_type").AsString(100).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("storage_locations")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable()
                .ForeignKey("fk_sl_project", "projects", "id")
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("description").AsString(2000).Nullable()
            .WithColumn("rfid_tag_uid").AsString(256).Nullable()
            .WithColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .WithColumn("image_content_type").AsString(100).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("dryers")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable()
                .ForeignKey("fk_dryers_project", "projects", "id")
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("description").AsString(2000).Nullable()
            .WithColumn("rfid_tag_uid").AsString(256).Nullable()
            .WithColumn("image_data").AsBinary(int.MaxValue).Nullable()
            .WithColumn("image_content_type").AsString(100).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("filament_materials")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).Nullable()
                .ForeignKey("fk_fm_project", "projects", "id")
            .WithColumn("type").AsString(100).NotNullable()
            .WithColumn("color_hex").AsString(7).NotNullable()
            .WithColumn("brand").AsString(200).NotNullable()
            .WithColumn("min_temp_celsius").AsInt32().NotNullable()
            .WithColumn("max_temp_celsius").AsInt32().NotNullable()
            .WithColumn("color_name").AsString(200).Nullable()
            .WithColumn("diameter_mm").AsDecimal(5, 2).NotNullable().WithDefaultValue(1.75)
            .WithColumn("weight_grams").AsDecimal(8, 2).Nullable()
            .WithColumn("bed_temp_celsius").AsInt32().Nullable()
            .WithColumn("density_g_cm3").AsDecimal(6, 4).Nullable()
            .WithColumn("dry_temp_celsius").AsInt32().Nullable()
            .WithColumn("dry_time_hours").AsDecimal(5, 1).Nullable()
            .WithColumn("notes").AsString(2000).Nullable()
            .WithColumn("reorder_url").AsString(2000).Nullable()
            .WithColumn("price_per_kg").AsDecimal(10, 2).Nullable()
            .WithColumn("is_public").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("spools")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable()
                .ForeignKey("fk_spools_project", "projects", "id")
            .WithColumn("filament_material_id").AsString(36).NotNullable()
                .ForeignKey("fk_spools_material", "filament_materials", "id")
            .WithColumn("rfid_tag_uid").AsString(256).Nullable()
            .WithColumn("opened_at").AsDateTime().Nullable()
            .WithColumn("repackaged_at").AsDateTime().Nullable()
            .WithColumn("reopened_at").AsDateTime().Nullable()
            .WithColumn("dried_at").AsDateTime().Nullable()
            .WithColumn("consumed_at").AsDateTime().Nullable()
            .WithColumn("remaining_weight_grams").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("remaining_percent").AsDecimal(5, 2).NotNullable().WithDefaultValue(100)
            .WithColumn("printer_id").AsString(36).Nullable()
                .ForeignKey("fk_spools_printer", "printers", "id")
            .WithColumn("storage_location_id").AsString(36).Nullable()
                .ForeignKey("fk_spools_storage", "storage_locations", "id")
            .WithColumn("dryer_id").AsString(36).Nullable()
                .ForeignKey("fk_spools_dryer", "dryers", "id")
            .WithColumn("purchased_at").AsDateTime().Nullable()
            .WithColumn("purchase_price").AsDecimal(10, 2).Nullable()
            .WithColumn("reorder_url").AsString(2000).Nullable()
            .WithColumn("notes").AsString(2000).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("invitations")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("project_id").AsString(36).NotNullable()
                .ForeignKey("fk_inv_project", "projects", "id")
            .WithColumn("invited_by_user_id").AsString(36).NotNullable()
                .ForeignKey("fk_inv_user", "users", "id")
            .WithColumn("token").AsString(128).NotNullable().Unique()
            .WithColumn("role").AsString(50).NotNullable()
            .WithColumn("expires_at").AsDateTime().NotNullable()
            .WithColumn("used_by_user_id").AsString(36).Nullable()
            .WithColumn("used_at").AsDateTime().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("audit_logs")
            .WithColumn("id").AsString(36).PrimaryKey()
            .WithColumn("timestamp").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("action").AsString(200).NotNullable()
            .WithColumn("user_id").AsString(36).Nullable()
            .WithColumn("username").AsString(100).Nullable()
            .WithColumn("entity_type").AsString(100).Nullable()
            .WithColumn("entity_id").AsString(36).Nullable()
            .WithColumn("entity_name").AsString(500).Nullable()
            .WithColumn("project_id").AsString(36).Nullable()
            .WithColumn("project_name").AsString(200).Nullable()
            .WithColumn("details").AsString(2000).Nullable()
            .WithColumn("ip_address").AsString(45).Nullable();
    }

    public override void Down()
    {
        Delete.Table("audit_logs");
        Delete.Table("invitations");
        Delete.Table("spools");
        Delete.Table("filament_materials");
        Delete.Table("project_members");
        Delete.Table("dryers");
        Delete.Table("storage_locations");
        Delete.Table("printers");
        Delete.Table("projects");
        Delete.Table("users");
    }
}
