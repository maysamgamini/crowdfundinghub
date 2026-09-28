using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
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
                name: "contributions");

            migrationBuilder.RenameTable(
                name: "contributions_processed_events",
                schema: "public",
                newName: "contributions_processed_events",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "contributions_outbox_messages",
                schema: "public",
                newName: "contributions_outbox_messages",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "contributions_dead_letter_events",
                schema: "public",
                newName: "contributions_dead_letter_events",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "contributions",
                schema: "public",
                newName: "contributions",
                newSchema: "contributions");

            migrationBuilder.RenameTable(
                name: "active_campaigns_cache",
                schema: "public",
                newName: "active_campaigns_cache",
                newSchema: "contributions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "contributions_processed_events",
                schema: "contributions",
                newName: "contributions_processed_events",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "contributions_outbox_messages",
                schema: "contributions",
                newName: "contributions_outbox_messages",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "contributions_dead_letter_events",
                schema: "contributions",
                newName: "contributions_dead_letter_events",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "contributions",
                schema: "contributions",
                newName: "contributions",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "active_campaigns_cache",
                schema: "contributions",
                newName: "active_campaigns_cache",
                newSchema: "public");
        }
    }
}
