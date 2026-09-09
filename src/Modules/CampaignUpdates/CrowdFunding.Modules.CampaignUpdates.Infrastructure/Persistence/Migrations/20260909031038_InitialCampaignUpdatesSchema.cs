using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCampaignUpdatesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign_owner_cache",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_owner_cache", x => x.CampaignId);
                });

            migrationBuilder.CreateTable(
                name: "webhook_delivery_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_delivery_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SecretKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ConsecutiveFailureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_tasks_Status_ScheduledAtUtc",
                table: "webhook_delivery_tasks",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_CampaignId_IsActive",
                table: "webhook_subscriptions",
                columns: new[] { "CampaignId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_owner_cache");

            migrationBuilder.DropTable(
                name: "webhook_delivery_tasks");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");
        }
    }
}
