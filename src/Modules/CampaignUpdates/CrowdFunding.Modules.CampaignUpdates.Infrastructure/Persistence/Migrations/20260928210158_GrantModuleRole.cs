using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Grants the module's runtime role, <c>campaignupdates_app</c>, DML on every table of the <c>campaignupdates</c>
    /// schema and nothing on the migrations history. Needed once because the earliest tables were
    /// created in <c>public</c> and moved here by <c>MoveTablesToModuleSchema</c>, so the schema's
    /// default privileges (set by <c>db/init/01-module-roles.sql</c>) never applied to them; tables
    /// created later inherit the grants by default privileges. Skipped when the role does not
    /// exist (a single-superuser setup); running the roles script later grants the same rights.
    /// See docs/data-ownership.md.
    /// </summary>
    public partial class GrantModuleRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $grant$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'campaignupdates_app') THEN
                        GRANT USAGE ON SCHEMA campaignupdates TO campaignupdates_app;
                        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA campaignupdates TO campaignupdates_app;
                        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA campaignupdates TO campaignupdates_app;
                        REVOKE ALL ON TABLE campaignupdates."__EFMigrationsHistory" FROM campaignupdates_app;
                    END IF;
                END
                $grant$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $revoke$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'campaignupdates_app') THEN
                        REVOKE SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA campaignupdates FROM campaignupdates_app;
                        REVOKE USAGE, SELECT ON ALL SEQUENCES IN SCHEMA campaignupdates FROM campaignupdates_app;
                    END IF;
                END
                $revoke$;
                """);
        }
    }
}
