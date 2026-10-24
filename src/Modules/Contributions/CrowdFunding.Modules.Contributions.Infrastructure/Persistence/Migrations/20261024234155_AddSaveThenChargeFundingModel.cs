using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaveThenChargeFundingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_contributions_one_live_pledge_per_backer",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.AddColumn<int>(
                name: "ChargeAttempts",
                schema: "contributions",
                table: "contributions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChargeLeaseUntilUtc",
                schema: "contributions",
                table: "contributions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextChargeAtUtc",
                schema: "contributions",
                table: "contributions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodReference",
                schema: "contributions",
                table: "contributions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "charge_attempts",
                schema: "contributions",
                columns: table => new
                {
                    ContributionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProviderPaymentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeclineCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charge_attempts", x => new { x.ContributionId, x.AttemptNumber });
                });

            migrationBuilder.CreateIndex(
                name: "ix_contributions_charging_lease",
                schema: "contributions",
                table: "contributions",
                column: "ChargeLeaseUntilUtc",
                filter: "\"Status\" = 'Charging'");

            migrationBuilder.CreateIndex(
                name: "ix_contributions_due_for_charge",
                schema: "contributions",
                table: "contributions",
                column: "NextChargeAtUtc",
                filter: "\"Status\" = 'ToCharge'");

            migrationBuilder.CreateIndex(
                name: "UX_contributions_one_live_pledge_per_backer",
                schema: "contributions",
                table: "contributions",
                columns: new[] { "CampaignId", "ContributorId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Active', 'ToCharge', 'Charging', 'Succeeded')");

            migrationBuilder.CreateIndex(
                name: "IX_charge_attempts_ProviderKey",
                schema: "contributions",
                table: "charge_attempts",
                column: "ProviderKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_charge_attempts_one_success_per_pledge",
                schema: "contributions",
                table: "charge_attempts",
                column: "ContributionId",
                unique: true,
                filter: "\"Status\" = 'Succeeded'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charge_attempts",
                schema: "contributions");

            migrationBuilder.DropIndex(
                name: "ix_contributions_charging_lease",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropIndex(
                name: "ix_contributions_due_for_charge",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropIndex(
                name: "UX_contributions_one_live_pledge_per_backer",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "ChargeAttempts",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "ChargeLeaseUntilUtc",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "NextChargeAtUtc",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "PaymentMethodReference",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.CreateIndex(
                name: "UX_contributions_one_live_pledge_per_backer",
                schema: "contributions",
                table: "contributions",
                columns: new[] { "CampaignId", "ContributorId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Succeeded')");
        }
    }
}
