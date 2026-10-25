using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payouts",
                schema: "pledges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PlatformFeeBps = table.Column<int>(type: "integer", nullable: true),
                    Failures = table.Column<int>(type: "integer", nullable: false),
                    LastFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderPayoutId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                    // NOTE: `xmin` is a PostgreSQL system column present on every table; the concurrency token maps onto
                    // it, so the generated column definition is removed (see 20260908215419_AddPledgeConcurrencyToken).
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payouts", x => x.Id);
                    table.CheckConstraint("CK_payouts_amounts_add_up", "(\"GrossAmount\" IS NULL AND \"FeeAmount\" IS NULL AND \"NetAmount\" IS NULL) OR (\"GrossAmount\" >= 0 AND \"FeeAmount\" >= 0 AND \"FeeAmount\" <= \"GrossAmount\" AND \"NetAmount\" = \"GrossAmount\" - \"FeeAmount\")");
                });

            migrationBuilder.CreateIndex(
                name: "IX_payouts_open",
                schema: "pledges",
                table: "payouts",
                column: "NextAttemptAtUtc",
                filter: "\"Status\" IN ('Pending', 'Ready')");

            migrationBuilder.CreateIndex(
                name: "UX_payouts_one_per_campaign",
                schema: "pledges",
                table: "payouts",
                column: "CampaignId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payouts",
                schema: "pledges");
        }
    }
}
