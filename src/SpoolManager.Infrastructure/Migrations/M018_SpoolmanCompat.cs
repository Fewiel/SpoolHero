using FluentMigrator;
using MySqlConnector;

namespace SpoolManager.Infrastructure.Migrations;

// Spoolman API compatibility: spools need a stable integer id (Spoolman clients like Moonraker
// address spools by int), and projects get API keys that are embedded in the URL path.
[Migration(18, TransactionBehavior.None)]
public class M018_SpoolmanCompat : Migration
{
    public override void Up()
    {
        // Add nullable first, backfill existing spools in creation order, then turn it into a
        // unique AUTO_INCREMENT column (MySQL/MariaDB continues the counter at MAX + 1).
        Execute.Sql("ALTER TABLE spools ADD COLUMN spoolman_id INT NULL");
        Execute.Sql(@"
            UPDATE spools s
            JOIN (SELECT id, ROW_NUMBER() OVER (ORDER BY created_at, id) AS rn FROM spools) x ON s.id = x.id
            SET s.spoolman_id = x.rn");
        Execute.Sql("ALTER TABLE spools MODIFY spoolman_id INT NOT NULL AUTO_INCREMENT, ADD UNIQUE KEY ux_spools_spoolman_id (spoolman_id)");

        Execute.WithConnection((conn, tx) =>
        {
            var mysqlConn = (MySqlConnection)conn;

            // projects.id may be utf8mb3 or utf8mb4 depending on the installation's history (see M014).
            // The FK column must use exactly the same charset/collation, so read it from the schema.
            var projectIdType = "VARCHAR(36)";
            using (var cmd = new MySqlCommand(@"
                SELECT CHARACTER_SET_NAME, COLLATION_NAME FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'projects' AND COLUMN_NAME = 'id'", mysqlConn))
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read() && !reader.IsDBNull(0) && !reader.IsDBNull(1))
                    projectIdType = $"VARCHAR(36) CHARACTER SET {reader.GetString(0)} COLLATE {reader.GetString(1)}";
            }

            using var create = new MySqlCommand($@"
                CREATE TABLE spoolman_api_keys (
                    id VARCHAR(36) NOT NULL,
                    project_id {projectIdType} NOT NULL,
                    api_key VARCHAR(64) NOT NULL,
                    name VARCHAR(100) NOT NULL,
                    created_at DATETIME NOT NULL,
                    last_used_at DATETIME NULL,
                    PRIMARY KEY (id),
                    UNIQUE KEY ux_spoolman_api_keys_api_key (api_key),
                    KEY ix_spoolman_api_keys_project (project_id),
                    CONSTRAINT fk_spoolman_api_keys_project FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci", mysqlConn);
            create.ExecuteNonQuery();
        });
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS spoolman_api_keys");
        Execute.Sql("ALTER TABLE spools DROP INDEX ux_spools_spoolman_id, DROP COLUMN spoolman_id");
    }
}
