using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToNotificationsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.RenameTable(
                name: "notification_preferences",
                newName: "notification_preferences",
                newSchema: "notifications");

            migrationBuilder.RenameTable(
                name: "campaign_title_cache",
                newName: "campaign_title_cache",
                newSchema: "notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "notification_preferences",
                schema: "notifications",
                newName: "notification_preferences");

            migrationBuilder.RenameTable(
                name: "campaign_title_cache",
                schema: "notifications",
                newName: "campaign_title_cache");
        }
    }
}
