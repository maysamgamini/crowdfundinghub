using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: `xmin` is a reserved PostgreSQL system column that already exists on every
            // table — it cannot be added via ALTER TABLE ... ADD COLUMN (Postgres rejects the
            // reserved name). ContributionConfiguration.cs maps EF's concurrency token directly
            // onto that existing system column, so no DDL is needed here; the auto-generated
            // AddColumn/DropColumn calls for "xmin" have been removed from this migration (see
            // the equivalent fix for Campaign in 20260908183629_AddCampaignConcurrencyAndLedger).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
