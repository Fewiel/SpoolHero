using FluentMigrator;
using MySqlConnector;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(14, TransactionBehavior.None)]
public class M014_UnicodeAndFixes : ForwardOnlyMigration
{
    private static readonly string[] Tables =
    [
        "users", "projects", "project_members", "invitations",
        "filament_materials", "spools", "printers", "storage_locations",
        "dryers", "support_tickets", "ticket_comments",
        "material_suggestions", "audit_logs", "site_settings", "smtp_settings"
    ];

    public override void Up()
    {
        Execute.WithConnection((conn, _) =>
        {
            var mysqlConn = (MySqlConnection)conn;
            using var cmd = new MySqlCommand { Connection = mysqlConn };

            cmd.CommandText = "SET FOREIGN_KEY_CHECKS=0";
            cmd.ExecuteNonQuery();

            foreach (var table in Tables)
            {
                try
                {
                    cmd.CommandText = $"ALTER TABLE `{table}` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                }
            }

            cmd.CommandText = "SET FOREIGN_KEY_CHECKS=1";
            cmd.ExecuteNonQuery();
        });
    }
}
