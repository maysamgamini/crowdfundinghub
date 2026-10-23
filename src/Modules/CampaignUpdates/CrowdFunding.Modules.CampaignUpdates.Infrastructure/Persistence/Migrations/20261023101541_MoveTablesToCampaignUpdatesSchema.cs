using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToCampaignUpdatesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "campaignupdates");

            migrationBuilder.RenameTable(
                name: "webhook_subscriptions",
                newName: "webhook_subscriptions",
                newSchema: "campaignupdates");

            migrationBuilder.RenameTable(
                name: "webhook_delivery_tasks",
                newName: "webhook_delivery_tasks",
                newSchema: "campaignupdates");

            migrationBuilder.RenameTable(
                name: "campaign_owner_cache",
                newName: "campaign_owner_cache",
                newSchema: "campaignupdates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "webhook_subscriptions",
                schema: "campaignupdates",
                newName: "webhook_subscriptions");

            migrationBuilder.RenameTable(
                name: "webhook_delivery_tasks",
                schema: "campaignupdates",
                newName: "webhook_delivery_tasks");

            migrationBuilder.RenameTable(
                name: "campaign_owner_cache",
                schema: "campaignupdates",
                newName: "campaign_owner_cache");
        }
    }
}
