using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignConcurrencyAndLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: `xmin` is a reserved PostgreSQL system column that already exists on every
            // table — it cannot be added via ALTER TABLE ... ADD COLUMN (Postgres rejects the
            // reserved name). CampaignConfiguration.cs maps EF's concurrency token directly onto
            // that existing system column, so no DDL is needed here; the auto-generated
            // AddColumn/DropColumn calls for "xmin" have been removed from this migration.
            migrationBuilder.CreateTable(
                name: "campaign_pledges_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PledgeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_pledges_ledger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_pledges_ledger_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_pledges_ledger_CampaignId",
                table: "campaign_pledges_ledger",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "uq_campaign_pledges_ledger_pledge_id",
                table: "campaign_pledges_ledger",
                column: "PledgeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_pledges_ledger");
        }
    }
}
