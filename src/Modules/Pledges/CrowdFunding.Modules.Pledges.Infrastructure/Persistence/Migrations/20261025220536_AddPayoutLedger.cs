using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payout_ledger_entries",
                schema: "pledges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PledgeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payout_ledger_entries", x => x.Id);
                    table.CheckConstraint("CK_payout_ledger_sign_matches_kind", "(\"Kind\" IN ('Charge', 'FeeReturned') AND \"Amount\" > 0) OR (\"Kind\" IN ('PlatformFee', 'PaidOut', 'ChargeRefunded') AND \"Amount\" < 0)");
                    table.ForeignKey(
                        name: "FK_payout_ledger_entries_payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalSchema: "pledges",
                        principalTable: "payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_payout_ledger_one_line_per_payout",
                schema: "pledges",
                table: "payout_ledger_entries",
                columns: new[] { "PayoutId", "Kind" },
                unique: true,
                filter: "\"PledgeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_payout_ledger_one_line_per_pledge",
                schema: "pledges",
                table: "payout_ledger_entries",
                columns: new[] { "PayoutId", "Kind", "PledgeId" },
                unique: true,
                filter: "\"PledgeId\" IS NOT NULL");

            // Append-only: a ledger is a record, not a table of values to correct. The only way to undo a line is a
            // new compensating line, so any UPDATE or DELETE is refused (the same rule as the campaign ledger).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION pledges.reject_payout_ledger_change() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'payout_ledger_entries is append-only: % is not allowed. Add a compensating line instead.', TG_OP
                        USING ERRCODE = '55000';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_payout_ledger_entries_append_only
                    BEFORE UPDATE OR DELETE ON pledges.payout_ledger_entries
                    FOR EACH ROW EXECUTE FUNCTION pledges.reject_payout_ledger_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_payout_ledger_entries_append_only ON pledges.payout_ledger_entries;
                DROP FUNCTION IF EXISTS pledges.reject_payout_ledger_change();
                """);

            migrationBuilder.DropTable(
                name: "payout_ledger_entries",
                schema: "pledges");
        }
    }
}
