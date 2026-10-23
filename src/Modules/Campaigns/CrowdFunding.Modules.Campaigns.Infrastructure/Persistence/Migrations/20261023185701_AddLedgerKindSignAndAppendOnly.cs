using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerKindSignAndAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_campaign_contributions_ledger_contribution_id",
                schema: "campaigns",
                table: "campaign_contributions_ledger");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "campaigns",
                table: "campaign_contributions_ledger",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Pledge");

            migrationBuilder.CreateIndex(
                name: "uq_campaign_contributions_ledger_contribution_kind",
                schema: "campaigns",
                table: "campaign_contributions_ledger",
                columns: new[] { "ContributionId", "Kind" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaign_contributions_ledger_sign_matches_kind",
                schema: "campaigns",
                table: "campaign_contributions_ledger",
                sql: "(\"Kind\" = 'Pledge' AND \"Amount\" > 0) OR (\"Kind\" = 'Refund' AND \"Amount\" < 0)");

            // Append-only: a ledger is a record, not a table of values to correct. The only way to undo an
            // entry is a new compensating entry, so any UPDATE or DELETE is refused.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION campaigns.reject_ledger_change() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'campaign_contributions_ledger is append-only: % is not allowed. Add a compensating entry instead.', TG_OP
                        USING ERRCODE = '55000';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_campaign_contributions_ledger_append_only
                    BEFORE UPDATE OR DELETE ON campaigns.campaign_contributions_ledger
                    FOR EACH ROW EXECUTE FUNCTION campaigns.reject_ledger_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER trg_campaign_contributions_ledger_append_only ON campaigns.campaign_contributions_ledger;
                DROP FUNCTION campaigns.reject_ledger_change();
                """);

            migrationBuilder.DropIndex(
                name: "uq_campaign_contributions_ledger_contribution_kind",
                schema: "campaigns",
                table: "campaign_contributions_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_campaign_contributions_ledger_sign_matches_kind",
                schema: "campaigns",
                table: "campaign_contributions_ledger");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "campaigns",
                table: "campaign_contributions_ledger");

            migrationBuilder.CreateIndex(
                name: "uq_campaign_contributions_ledger_contribution_id",
                schema: "campaigns",
                table: "campaign_contributions_ledger",
                column: "ContributionId",
                unique: true);
        }
    }
}
