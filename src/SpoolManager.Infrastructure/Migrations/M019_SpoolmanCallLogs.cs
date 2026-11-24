using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(19)]
public class M019_SpoolmanCallLogs : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            CREATE TABLE spoolman_call_logs (
                id VARCHAR(36) NOT NULL,
                api_key_id VARCHAR(36) NOT NULL,
                called_at DATETIME NOT NULL,
                method VARCHAR(10) NOT NULL,
                path VARCHAR(255) NOT NULL,
                status_code INT NOT NULL,
                PRIMARY KEY (id),
                KEY ix_spoolman_call_logs_key_time (api_key_id, called_at),
                KEY ix_spoolman_call_logs_time (called_at),
                CONSTRAINT fk_spoolman_call_logs_key FOREIGN KEY (api_key_id) REFERENCES spoolman_api_keys (id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS spoolman_call_logs");
    }
}
