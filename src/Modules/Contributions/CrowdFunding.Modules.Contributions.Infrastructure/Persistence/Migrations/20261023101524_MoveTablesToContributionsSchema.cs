using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToContributionsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contributions");

            migrationBuilder.RenameTable(
                name: "processed_payment_webhooks",
                newName: "processed_payment_webhooks",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "contributions_outbox_messages",
                newName: "contributions_outbox_messages",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "contributions_dead_letter_events",
                newName: "contributions_dead_letter_events",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "contributions",
                newName: "contributions",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "active_campaigns_cache",
                newName: "active_campaigns_cache",
                newSchema: "contributions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "processed_payment_webhooks",
                schema: "contributions",
                newName: "processed_payment_webhooks");

            migrationBuilder.RenameTable(
                name: "contributions_outbox_messages",
                schema: "contributions",
                newName: "contributions_outbox_messages");

            migrationBuilder.RenameTable(
                name: "contributions_dead_letter_events",
                schema: "contributions",
                newName: "contributions_dead_letter_events");

            migrationBuilder.RenameTable(
                name: "contributions",
                schema: "contributions",
                newName: "contributions");

            migrationBuilder.RenameTable(
                name: "active_campaigns_cache",
                schema: "contributions",
                newName: "active_campaigns_cache");
        }
    }
}
