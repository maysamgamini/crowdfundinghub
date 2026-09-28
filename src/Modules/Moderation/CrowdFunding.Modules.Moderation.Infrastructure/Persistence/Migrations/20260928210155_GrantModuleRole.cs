using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Grants the module's runtime role, <c>moderation_app</c>, DML on every table of the <c>moderation</c>
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
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'moderation_app') THEN
                        GRANT USAGE ON SCHEMA moderation TO moderation_app;
                        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA moderation TO moderation_app;
                        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA moderation TO moderation_app;
                        REVOKE ALL ON TABLE moderation."__EFMigrationsHistory" FROM moderation_app;
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
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'moderation_app') THEN
                        REVOKE SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA moderation FROM moderation_app;
                        REVOKE USAGE, SELECT ON ALL SEQUENCES IN SCHEMA moderation FROM moderation_app;
                    END IF;
                END
                $revoke$;
                """);
        }
    }
}
