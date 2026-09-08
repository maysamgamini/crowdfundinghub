using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_contributions_CampaignId_CreatedAtUtc",
                table: "contributions",
                columns: new[] { "CampaignId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_contributions_ContributorId",
                table: "contributions",
                column: "ContributorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contributions_CampaignId_CreatedAtUtc",
                table: "contributions");

            migrationBuilder.DropIndex(
                name: "IX_contributions_ContributorId",
                table: "contributions");
        }
    }
}
