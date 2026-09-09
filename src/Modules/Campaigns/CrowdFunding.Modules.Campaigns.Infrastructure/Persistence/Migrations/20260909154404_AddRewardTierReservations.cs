using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardTierReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reward_tier_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardTierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    BackerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContributionId = table.Column<Guid>(type: "uuid", nullable: true),
                    // NOTE: `xmin` is a reserved PostgreSQL system column that already exists on
                    // every table — it cannot be added via CREATE TABLE (Postgres rejects the
                    // reserved name: "column name \"xmin\" conflicts with a system column name").
                    // RewardTierReservationConfiguration.cs maps EF's concurrency token directly
                    // onto that existing system column, so the auto-generated xmin column
                    // definition has been removed here (see the equivalent fix for RewardTier in
                    // 20260909032436_AddRewardTiers, which had the same latent bug).
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reward_tier_reservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reward_tier_reservations_RewardTierId",
                table: "reward_tier_reservations",
                column: "RewardTierId");

            migrationBuilder.CreateIndex(
                name: "IX_reward_tier_reservations_Status_ExpiresAtUtc",
                table: "reward_tier_reservations",
                columns: new[] { "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reward_tier_reservations");
        }
    }
}
