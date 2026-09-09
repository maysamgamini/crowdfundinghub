using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignReviewAutomatedAnalysisScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdultContentScore",
                table: "campaign_reviews",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ToxicityScore",
                table: "campaign_reviews",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdultContentScore",
                table: "campaign_reviews");

            migrationBuilder.DropColumn(
                name: "ToxicityScore",
                table: "campaign_reviews");
        }
    }
}
