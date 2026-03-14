using FluentMigrator;

namespace SpoolManager.Infrastructure.Migrations;

[Migration(11)]
public class M011_DashboardOnboardingDismissed : Migration
{
    public override void Up()
    {
        Alter.Table("users")
            .AddColumn("dashboard_onboarding_dismissed")
            .AsBoolean()
            .NotNullable()
            .WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("dashboard_onboarding_dismissed").FromTable("users");
    }
}
