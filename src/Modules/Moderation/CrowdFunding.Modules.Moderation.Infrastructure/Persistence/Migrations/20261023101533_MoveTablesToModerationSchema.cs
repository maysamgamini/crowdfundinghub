using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToModerationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "moderation");

            migrationBuilder.RenameTable(
                name: "moderation_outbox_messages",
                newName: "moderation_outbox_messages",
                newSchema: "moderation");

            migrationBuilder.RenameTable(
                name: "moderation_dead_letter_events",
                newName: "moderation_dead_letter_events",
                newSchema: "moderation");

            migrationBuilder.RenameTable(
                name: "campaign_reviews",
                newName: "campaign_reviews",
                newSchema: "moderation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "moderation_outbox_messages",
                schema: "moderation",
                newName: "moderation_outbox_messages");

            migrationBuilder.RenameTable(
                name: "moderation_dead_letter_events",
                schema: "moderation",
                newName: "moderation_dead_letter_events");

            migrationBuilder.RenameTable(
                name: "campaign_reviews",
                schema: "moderation",
                newName: "campaign_reviews");
        }
    }
}
