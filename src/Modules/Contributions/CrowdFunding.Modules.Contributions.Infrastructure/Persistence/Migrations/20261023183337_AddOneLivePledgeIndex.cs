using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOneLivePledgeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The rule is new. If existing data already breaks it (a backer with two live pledges on one
            // campaign), creating the index would fail with a vague duplicate-key error. Say what is wrong
            // and how to look at it instead. Resolving such pledges moves money, so it is a person's decision.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM contributions.contributions
                        WHERE "Status" IN ('Pending', 'Succeeded')
                        GROUP BY "CampaignId", "ContributorId"
                        HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Some backers have more than one live (Pending or Succeeded) pledge on the same campaign. Find them with: SELECT "CampaignId", "ContributorId", COUNT(*) FROM contributions.contributions WHERE "Status" IN (''Pending'', ''Succeeded'') GROUP BY 1, 2 HAVING COUNT(*) > 1; then refund or fail the extra pledges and run the migration again.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_contributions_one_live_pledge_per_backer",
                schema: "contributions",
                table: "contributions",
                columns: new[] { "CampaignId", "ContributorId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Succeeded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_contributions_one_live_pledge_per_backer",
                schema: "contributions",
                table: "contributions");
        }
    }
}
