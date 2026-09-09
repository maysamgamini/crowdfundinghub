using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookDeliveryTaskClaimLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                table: "webhook_delivery_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkerId",
                table: "webhook_delivery_tasks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "webhook_delivery_tasks");

            migrationBuilder.DropColumn(
                name: "WorkerId",
                table: "webhook_delivery_tasks");
        }
    }
}
