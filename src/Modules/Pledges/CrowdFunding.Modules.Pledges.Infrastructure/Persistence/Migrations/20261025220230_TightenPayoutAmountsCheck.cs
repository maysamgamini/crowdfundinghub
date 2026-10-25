using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TightenPayoutAmountsCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_payouts_amounts_add_up",
                schema: "pledges",
                table: "payouts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payouts_amounts_add_up",
                schema: "pledges",
                table: "payouts",
                sql: "(\"Status\" = 'Pending' AND \"GrossAmount\" IS NULL AND \"FeeAmount\" IS NULL AND \"NetAmount\" IS NULL) OR (\"Status\" IN ('Ready', 'Paid') AND \"GrossAmount\" IS NOT NULL AND \"FeeAmount\" IS NOT NULL AND \"NetAmount\" IS NOT NULL AND \"GrossAmount\" > 0 AND \"FeeAmount\" >= 0 AND \"FeeAmount\" <= \"GrossAmount\" AND \"NetAmount\" = \"GrossAmount\" - \"FeeAmount\") OR (\"Status\" = 'NothingToPay' AND \"GrossAmount\" = 0 AND \"FeeAmount\" = 0 AND \"NetAmount\" = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_payouts_amounts_add_up",
                schema: "pledges",
                table: "payouts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payouts_amounts_add_up",
                schema: "pledges",
                table: "payouts",
                sql: "(\"GrossAmount\" IS NULL AND \"FeeAmount\" IS NULL AND \"NetAmount\" IS NULL) OR (\"GrossAmount\" >= 0 AND \"FeeAmount\" >= 0 AND \"FeeAmount\" <= \"GrossAmount\" AND \"NetAmount\" = \"GrossAmount\" - \"FeeAmount\")");
        }
    }
}
