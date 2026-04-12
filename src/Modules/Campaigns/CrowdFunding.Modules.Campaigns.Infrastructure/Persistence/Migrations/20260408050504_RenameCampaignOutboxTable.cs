using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCampaignOutboxTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "outbox_messages",
                newName: "campaigns_outbox_messages");

            migrationBuilder.RenameIndex(
                name: "IX_outbox_messages_ProcessedOnUtc_OccurredOnUtc",
                table: "campaigns_outbox_messages",
                newName: "IX_campaigns_outbox_messages_ProcessedOnUtc_OccurredOnUtc");

            migrationBuilder.Sql("""
                ALTER TABLE campaigns_outbox_messages
                RENAME CONSTRAINT "PK_outbox_messages" TO "PK_campaigns_outbox_messages";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE campaigns_outbox_messages
                RENAME CONSTRAINT "PK_campaigns_outbox_messages" TO "PK_outbox_messages";
                """);

            migrationBuilder.RenameIndex(
                name: "IX_campaigns_outbox_messages_ProcessedOnUtc_OccurredOnUtc",
                table: "campaigns_outbox_messages",
                newName: "IX_outbox_messages_ProcessedOnUtc_OccurredOnUtc");

            migrationBuilder.RenameTable(
                name: "campaigns_outbox_messages",
                newName: "outbox_messages");
        }
    }
}
