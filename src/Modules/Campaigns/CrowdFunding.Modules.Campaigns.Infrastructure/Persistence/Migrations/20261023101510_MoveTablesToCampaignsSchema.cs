using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToCampaignsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "campaigns");

            migrationBuilder.RenameTable(
                name: "reward_tiers",
                newName: "reward_tiers",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "reward_tier_reservations",
                newName: "reward_tier_reservations",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaigns_outbox_messages",
                newName: "campaigns_outbox_messages",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaigns_dead_letter_events",
                newName: "campaigns_dead_letter_events",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaigns",
                newName: "campaigns",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaign_contributions_ledger",
                newName: "campaign_contributions_ledger",
                newSchema: "campaigns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "reward_tiers",
                schema: "campaigns",
                newName: "reward_tiers");

            migrationBuilder.RenameTable(
                name: "reward_tier_reservations",
                schema: "campaigns",
                newName: "reward_tier_reservations");

            migrationBuilder.RenameTable(
                name: "campaigns_outbox_messages",
                schema: "campaigns",
                newName: "campaigns_outbox_messages");

            migrationBuilder.RenameTable(
                name: "campaigns_dead_letter_events",
                schema: "campaigns",
                newName: "campaigns_dead_letter_events");

            migrationBuilder.RenameTable(
                name: "campaigns",
                schema: "campaigns",
                newName: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaign_contributions_ledger",
                schema: "campaigns",
                newName: "campaign_contributions_ledger");
        }
    }
}
