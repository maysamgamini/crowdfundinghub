using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToPledgesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pledges");

            migrationBuilder.RenameTable(
                name: "processed_payment_webhooks",
                newName: "processed_payment_webhooks",
                newSchema: "pledges");

            migrationBuilder.RenameTable(
                name: "pledges_outbox_messages",
                newName: "pledges_outbox_messages",
                newSchema: "pledges");

            migrationBuilder.RenameTable(
                name: "pledges_dead_letter_events",
                newName: "pledges_dead_letter_events",
                newSchema: "pledges");

            migrationBuilder.RenameTable(
                name: "pledges",
                newName: "pledges",
                newSchema: "pledges");

            migrationBuilder.RenameTable(
                name: "active_campaigns_cache",
                newName: "active_campaigns_cache",
                newSchema: "pledges");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "processed_payment_webhooks",
                schema: "pledges",
                newName: "processed_payment_webhooks");

            migrationBuilder.RenameTable(
                name: "pledges_outbox_messages",
                schema: "pledges",
                newName: "pledges_outbox_messages");

            migrationBuilder.RenameTable(
                name: "pledges_dead_letter_events",
                schema: "pledges",
                newName: "pledges_dead_letter_events");

            migrationBuilder.RenameTable(
                name: "pledges",
                schema: "pledges",
                newName: "pledges");

            migrationBuilder.RenameTable(
                name: "active_campaigns_cache",
                schema: "pledges",
                newName: "active_campaigns_cache");
        }
    }
}
