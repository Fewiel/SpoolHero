using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(7)]
public class M007_TicketAnswered : Migration
{
    public override void Up()
    {
        Alter.Table("ticket_comments")
            .AddColumn("is_internal").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("is_internal").FromTable("ticket_comments");
    }
}
