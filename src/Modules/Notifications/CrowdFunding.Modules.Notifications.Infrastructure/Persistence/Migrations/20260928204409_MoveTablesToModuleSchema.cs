using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.Migrations
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
                name: "notifications");

            migrationBuilder.RenameTable(
                name: "notification_preferences",
                schema: "public",
                newName: "notification_preferences",
                newSchema: "notifications");

            migrationBuilder.RenameTable(
                name: "campaign_title_cache",
                schema: "public",
                newName: "campaign_title_cache",
                newSchema: "notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "notification_preferences",
                schema: "notifications",
                newName: "notification_preferences",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaign_title_cache",
                schema: "notifications",
                newName: "campaign_title_cache",
                newSchema: "public");
        }
    }
}
