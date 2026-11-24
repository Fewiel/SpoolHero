using FluentMigrator;
using MySqlConnector;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(20)]
public class M020_Webhooks : Migration
{
    public override void Up()
    {
        Execute.WithConnection((conn, tx) =>
        {
            var mysqlConn = (MySqlConnection)conn;
            var mysqlTx = tx as MySqlTransaction;
            MySqlCommand Cmd(string sql) => new(sql, mysqlConn, mysqlTx);

            // A previous attempt of this migration could leave an empty table behind: MySQL commits
            // CREATE TABLE implicitly, so the failed foreign key did not roll it back.
            if (TableExists(Cmd, "webhooks"))
            {
                using var count = Cmd("SELECT COUNT(*) FROM webhooks");
                if (Convert.ToInt64(count.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("Table 'webhooks' already exists and contains data.");
                using var drop = Cmd("DROP TABLE webhooks");
                drop.ExecuteNonQuery();
            }

            // projects.id may be utf8mb3 or utf8mb4 depending on the installation's history (see M014).
            // The FK column must use exactly the same charset/collation, so read it from the schema.
            var projectIdType = "VARCHAR(36)";
            using (var cmd = Cmd(@"
                SELECT CHARACTER_SET_NAME, COLLATION_NAME FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'projects' AND COLUMN_NAME = 'id'"))
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read() && !reader.IsDBNull(0) && !reader.IsDBNull(1))
                    projectIdType = $"VARCHAR(36) CHARACTER SET {reader.GetString(0)} COLLATE {reader.GetString(1)}";
            }

            using var create = Cmd($@"
                CREATE TABLE webhooks (
                    id VARCHAR(36) NOT NULL,
                    project_id {projectIdType} NOT NULL,
                    name VARCHAR(200) NOT NULL,
                    type VARCHAR(30) NOT NULL,
                    url VARCHAR(2000) NULL,
                    bot_token VARCHAR(200) NULL,
                    chat_id VARCHAR(100) NULL,
                    is_enabled TINYINT(1) NOT NULL DEFAULT 1,
                    on_dryer_finished TINYINT(1) NOT NULL DEFAULT 1,
                    on_spool_low TINYINT(1) NOT NULL DEFAULT 1,
                    on_spool_consumed TINYINT(1) NOT NULL DEFAULT 1,
                    last_triggered_at DATETIME NULL,
                    last_success TINYINT(1) NULL,
                    last_status_code INT NULL,
                    last_error VARCHAR(1000) NULL,
                    created_at DATETIME NOT NULL,
                    updated_at DATETIME NOT NULL,
                    PRIMARY KEY (id),
                    KEY ix_webhooks_project (project_id),
                    CONSTRAINT fk_webhooks_project FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
            create.ExecuteNonQuery();
        });
    }

    public override void Down()
    {
        Delete.Table("webhooks");
    }

    private static bool TableExists(Func<string, MySqlCommand> cmdFactory, string table)
    {
        using var cmd = cmdFactory(
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t");
        cmd.Parameters.AddWithValue("@t", table);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}
