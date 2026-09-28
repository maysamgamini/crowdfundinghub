using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Migrations
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
                name: "moderation");

            migrationBuilder.RenameTable(
                name: "moderation_outbox_messages",
                schema: "public",
                newName: "moderation_outbox_messages",
                newSchema: "moderation");

            migrationBuilder.RenameTable(
                name: "moderation_dead_letter_events",
                schema: "public",
                newName: "moderation_dead_letter_events",
                newSchema: "moderation");

            migrationBuilder.RenameTable(
                name: "campaign_reviews",
                schema: "public",
                newName: "campaign_reviews",
                newSchema: "moderation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "moderation_outbox_messages",
                schema: "moderation",
                newName: "moderation_outbox_messages",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "moderation_dead_letter_events",
                schema: "moderation",
                newName: "moderation_dead_letter_events",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaign_reviews",
                schema: "moderation",
                newName: "campaign_reviews",
                newSchema: "public");
        }
    }
}
