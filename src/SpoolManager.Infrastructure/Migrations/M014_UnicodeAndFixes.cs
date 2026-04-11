using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(14)]
public class M014_UnicodeAndFixes : Migration
{
    public override void Up()
    {
        Execute.Sql("ALTER TABLE `projects` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `filament_materials` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `spools` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `printers` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `storage_locations` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `dryers` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `users` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `support_tickets` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `ticket_comments` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `material_suggestions` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `audit_logs` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        Execute.Sql("ALTER TABLE `site_settings` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
    }

    public override void Down()
    {
    }
}
