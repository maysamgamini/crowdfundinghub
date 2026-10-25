using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPledgeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_pledges_CampaignId_CreatedAtUtc",
                table: "pledges",
                columns: new[] { "CampaignId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_pledges_BackerId",
                table: "pledges",
                column: "BackerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pledges_CampaignId_CreatedAtUtc",
                table: "pledges");

            migrationBuilder.DropIndex(
                name: "IX_pledges_BackerId",
                table: "pledges");
        }
    }
}
