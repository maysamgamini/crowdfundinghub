using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppendOnlyCampaignLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign_ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_ledger_entries", x => x.Id);
                    table.CheckConstraint("ck_campaign_ledger_entries_kind_sign", "(\"Kind\" = 0 AND \"Amount\" > 0 AND \"ContributionId\" IS NOT NULL) OR (\"Kind\" = 1 AND \"Amount\" < 0 AND \"ContributionId\" IS NOT NULL) OR (\"Kind\" = 2 AND \"ContributionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_campaign_ledger_entries_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campaign_ledger_entries_campaign_id",
                table: "campaign_ledger_entries",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "uq_campaign_ledger_entries_contribution_kind",
                table: "campaign_ledger_entries",
                columns: new[] { "ContributionId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_campaign_ledger_entries_opening_balance",
                table: "campaign_ledger_entries",
                column: "CampaignId",
                unique: true,
                filter: "\"Kind\" = 2");

            // Backfill 1: every contribution already credited becomes a Pledge entry.
            migrationBuilder.Sql(
                """
                INSERT INTO campaign_ledger_entries ("Id", "CampaignId", "ContributionId", "Kind", "Amount", "Currency", "RecordedAtUtc")
                SELECT "Id", "CampaignId", "ContributionId", 0, "Amount", "Currency", "RecordedAtUtc"
                FROM campaign_contributions_ledger;
                """);

            // Backfill 2: whatever part of a campaign's raised_amount is not explained by those
            // pledges (data credited before the dedupe table existed, or seeded directly) becomes
            // one OpeningBalance entry, so raised_amount = SUM(entries) holds for every campaign
            // from the first moment the ledger is the source of truth.
            migrationBuilder.Sql(
                """
                INSERT INTO campaign_ledger_entries ("Id", "CampaignId", "ContributionId", "Kind", "Amount", "Currency", "RecordedAtUtc")
                SELECT gen_random_uuid(), c."Id", NULL, 2, c.raised_amount - COALESCE(p.total, 0), c.raised_currency, now()
                FROM campaigns c
                LEFT JOIN (
                    SELECT "CampaignId", SUM("Amount") AS total
                    FROM campaign_ledger_entries
                    GROUP BY "CampaignId") p ON p."CampaignId" = c."Id"
                WHERE c.raised_amount <> COALESCE(p.total, 0);
                """);

            migrationBuilder.DropTable(
                name: "campaign_contributions_ledger");

            // Append-only, enforced by the database for every client (application, scripts, psql):
            // a correction is a new compensating entry, never an edit of history.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION campaign_ledger_entries_reject_changes() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'campaign_ledger_entries is append-only: % is not allowed; append a compensating entry instead', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER campaign_ledger_entries_append_only
                BEFORE UPDATE OR DELETE ON campaign_ledger_entries
                FOR EACH ROW EXECUTE FUNCTION campaign_ledger_entries_reject_changes();
                """);

            // The snapshot invariant, enforced by the database for every writer: raised_amount
            // can only ever hold SUM(ledger entries). A new campaign starts at zero, and any
            // UPDATE that changes raised_amount to anything else (a stale ORM write, a hand-edited
            // row) is rejected. CampaignLedger's recompute statement is the one write that passes.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION campaigns_raised_amount_matches_ledger() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    ledger_total numeric(18,2);
                BEGIN
                    SELECT COALESCE(SUM("Amount"), 0) INTO ledger_total
                    FROM campaign_ledger_entries WHERE "CampaignId" = NEW."Id";

                    IF NEW.raised_amount <> ledger_total THEN
                        RAISE EXCEPTION 'campaigns.raised_amount (%) must equal the sum of campaign_ledger_entries (%); append a ledger entry instead', NEW.raised_amount, ledger_total
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER campaigns_raised_amount_on_insert
                BEFORE INSERT ON campaigns
                FOR EACH ROW EXECUTE FUNCTION campaigns_raised_amount_matches_ledger();

                CREATE TRIGGER campaigns_raised_amount_on_update
                BEFORE UPDATE OF raised_amount ON campaigns
                FOR EACH ROW WHEN (OLD.raised_amount IS DISTINCT FROM NEW.raised_amount)
                EXECUTE FUNCTION campaigns_raised_amount_matches_ledger();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER campaigns_raised_amount_on_update ON campaigns;
                DROP TRIGGER campaigns_raised_amount_on_insert ON campaigns;
                DROP FUNCTION campaigns_raised_amount_matches_ledger();
                DROP TRIGGER campaign_ledger_entries_append_only ON campaign_ledger_entries;
                DROP FUNCTION campaign_ledger_entries_reject_changes();
                """);

            migrationBuilder.CreateTable(
                name: "campaign_contributions_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_contributions_ledger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_contributions_ledger_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_contributions_ledger_CampaignId",
                table: "campaign_contributions_ledger",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "uq_campaign_contributions_ledger_contribution_id",
                table: "campaign_contributions_ledger",
                column: "ContributionId",
                unique: true);

            // Only pledges fit the old dedupe table. Refunds and opening balances have no
            // equivalent there; raised_amount keeps its current (net) value.
            migrationBuilder.Sql(
                """
                INSERT INTO campaign_contributions_ledger ("Id", "Amount", "CampaignId", "ContributionId", "Currency", "RecordedAtUtc")
                SELECT "Id", "Amount", "CampaignId", "ContributionId", "Currency", "RecordedAtUtc"
                FROM campaign_ledger_entries
                WHERE "Kind" = 0;
                """);

            migrationBuilder.DropTable(
                name: "campaign_ledger_entries");
        }
    }
}
