using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(1)]
public class M001_InitialSchema : Migration
{
    public override void Up()
    {
        Create.Table("users")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("username").AsString(100).NotNullable().Unique()
            .WithColumn("email").AsString(256).NotNullable().Unique()
            .WithColumn("password_hash").AsString(256).NotNullable()
            .WithColumn("is_platform_admin").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("projects")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("description").AsString(2000).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("project_members")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("project_id").AsInt32().NotNullable()
                .ForeignKey("fk_project_members_project", "projects", "id")
            .WithColumn("user_id").AsInt32().NotNullable()
                .ForeignKey("fk_project_members_user", "users", "id")
            .WithColumn("role").AsString(20).NotNullable().WithDefaultValue("member")
            .WithColumn("joined_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("idx_project_members_project").OnTable("project_members").OnColumn("project_id");
        Create.Index("idx_project_members_user").OnTable("project_members").OnColumn("user_id");
        Create.Index("idx_project_members_unique").OnTable("project_members")
            .OnColumn("project_id").Ascending()
            .OnColumn("user_id").Ascending()
            .WithOptions().Unique();

        Create.Table("invitations")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("project_id").AsInt32().NotNullable()
                .ForeignKey("fk_invitations_project", "projects", "id")
            .WithColumn("invited_by_user_id").AsInt32().NotNullable()
                .ForeignKey("fk_invitations_inviter", "users", "id")
            .WithColumn("token").AsString(64).NotNullable().Unique()
            .WithColumn("role").AsString(20).NotNullable().WithDefaultValue("member")
            .WithColumn("used_by_user_id").AsInt32().Nullable()
            .WithColumn("used_at").AsDateTime().Nullable()
            .WithColumn("expires_at").AsDateTime().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("filament_materials")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("project_id").AsInt32().Nullable()
                .ForeignKey("fk_materials_project", "projects", "id")
            .WithColumn("type").AsString(50).NotNullable()
            .WithColumn("color_hex").AsString(6).NotNullable().WithDefaultValue("FFFFFF")
            .WithColumn("brand").AsString(100).NotNullable()
            .WithColumn("min_temp_celsius").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("max_temp_celsius").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("color_name").AsString(100).Nullable()
            .WithColumn("diameter_mm").AsDecimal(4, 2).NotNullable().WithDefaultValue(1.75)
            .WithColumn("weight_grams").AsInt32().Nullable()
            .WithColumn("bed_temp_celsius").AsInt32().Nullable()
            .WithColumn("density_g_cm3").AsDecimal(6, 4).Nullable()
            .WithColumn("dry_temp_celsius").AsInt32().Nullable()
            .WithColumn("dry_time_hours").AsInt32().Nullable()
            .WithColumn("notes").AsString(2000).Nullable()
            .WithColumn("reorder_url").AsString(2000).Nullable()
            .WithColumn("price_per_kg").AsDecimal(10, 2).Nullable()
            .WithColumn("is_public").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("idx_materials_project").OnTable("filament_materials").OnColumn("project_id");

        Create.Table("printers")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("project_id").AsInt32().NotNullable()
                .ForeignKey("fk_printers_project", "projects", "id")
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("notes").AsString(2000).Nullable()
            .WithColumn("rfid_tag_uid").AsString(100).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("idx_printers_project").OnTable("printers").OnColumn("project_id");
        Create.Index("idx_printers_rfid").OnTable("printers").OnColumn("rfid_tag_uid");

        Create.Table("storage_locations")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("project_id").AsInt32().NotNullable()
                .ForeignKey("fk_storage_project", "projects", "id")
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("description").AsString(2000).Nullable()
            .WithColumn("rfid_tag_uid").AsString(100).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("idx_storage_project").OnTable("storage_locations").OnColumn("project_id");
        Create.Index("idx_storage_rfid").OnTable("storage_locations").OnColumn("rfid_tag_uid");

        Create.Table("dryers")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("project_id").AsInt32().NotNullable()
                .ForeignKey("fk_dryers_project", "projects", "id")
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("description").AsString(2000).Nullable()
            .WithColumn("rfid_tag_uid").AsString(100).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("idx_dryers_project").OnTable("dryers").OnColumn("project_id");
        Create.Index("idx_dryers_rfid").OnTable("dryers").OnColumn("rfid_tag_uid");

        Create.Table("spools")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("project_id").AsInt32().NotNullable()
                .ForeignKey("fk_spools_project", "projects", "id")
            .WithColumn("filament_material_id").AsInt32().NotNullable()
                .ForeignKey("fk_spools_material", "filament_materials", "id")
            .WithColumn("rfid_tag_uid").AsString(100).Nullable()
            .WithColumn("opened_at").AsDateTime().Nullable()
            .WithColumn("repackaged_at").AsDateTime().Nullable()
            .WithColumn("reopened_at").AsDateTime().Nullable()
            .WithColumn("dried_at").AsDateTime().Nullable()
            .WithColumn("consumed_at").AsDateTime().Nullable()
            .WithColumn("remaining_weight_grams").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("remaining_percent").AsDecimal(5, 2).NotNullable().WithDefaultValue(100)
            .WithColumn("printer_id").AsInt32().Nullable()
                .ForeignKey("fk_spools_printer", "printers", "id")
            .WithColumn("storage_location_id").AsInt32().Nullable()
                .ForeignKey("fk_spools_storage", "storage_locations", "id")
            .WithColumn("dryer_id").AsInt32().Nullable()
                .ForeignKey("fk_spools_dryer", "dryers", "id")
            .WithColumn("purchased_at").AsDateTime().Nullable()
            .WithColumn("purchase_price").AsDecimal(10, 2).Nullable()
            .WithColumn("reorder_url").AsString(2000).Nullable()
            .WithColumn("notes").AsString(2000).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .WithColumn("updated_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Index("idx_spools_project").OnTable("spools").OnColumn("project_id");
        Create.Index("idx_spools_material").OnTable("spools").OnColumn("filament_material_id");
        Create.Index("idx_spools_printer").OnTable("spools").OnColumn("printer_id");
        Create.Index("idx_spools_storage").OnTable("spools").OnColumn("storage_location_id");
        Create.Index("idx_spools_dryer").OnTable("spools").OnColumn("dryer_id");
        Create.Index("idx_spools_rfid").OnTable("spools").OnColumn("rfid_tag_uid");
    }

    public override void Down()
    {
        Delete.Table("spools");
        Delete.Table("dryers");
        Delete.Table("storage_locations");
        Delete.Table("printers");
        Delete.Table("filament_materials");
        Delete.Table("invitations");
        Delete.Table("project_members");
        Delete.Table("projects");
        Delete.Table("users");
    }
}
