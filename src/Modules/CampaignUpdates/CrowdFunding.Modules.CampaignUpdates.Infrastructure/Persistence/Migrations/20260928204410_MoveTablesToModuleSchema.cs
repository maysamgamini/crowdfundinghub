using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves every table this module owns from <c>public</c> into the module's own schema
    /// (<c>ALTER TABLE … SET SCHEMA</c>). A pure metadata change: rows, indexes, constraints,
    /// owned sequences and triggers move with their table, and nothing is copied. See
    /// docs/data-ownership.md.
    /// </summary>
    public partial class MoveTablesToModuleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "campaignupdates");

            migrationBuilder.RenameTable(
                name: "webhook_subscriptions",
                schema: "public",
                newName: "webhook_subscriptions",
                newSchema: "campaignupdates");

            migrationBuilder.RenameTable(
                name: "webhook_delivery_tasks",
                schema: "public",
                newName: "webhook_delivery_tasks",
                newSchema: "campaignupdates");

            migrationBuilder.RenameTable(
                name: "campaign_updates_processed_events",
                schema: "public",
                newName: "campaign_updates_processed_events",
                newSchema: "campaignupdates");

            migrationBuilder.RenameTable(
                name: "campaign_owner_cache",
                schema: "public",
                newName: "campaign_owner_cache",
                newSchema: "campaignupdates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "webhook_subscriptions",
                schema: "campaignupdates",
                newName: "webhook_subscriptions",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "webhook_delivery_tasks",
                schema: "campaignupdates",
                newName: "webhook_delivery_tasks",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaign_updates_processed_events",
                schema: "campaignupdates",
                newName: "campaign_updates_processed_events",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaign_owner_cache",
                schema: "campaignupdates",
                newName: "campaign_owner_cache",
                newSchema: "public");
        }
    }
}
