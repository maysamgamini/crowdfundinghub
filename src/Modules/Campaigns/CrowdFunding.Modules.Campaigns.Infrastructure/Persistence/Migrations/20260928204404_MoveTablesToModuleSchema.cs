using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves every table this module owns from <c>public</c> into the module's own schema
    /// (<c>ALTER TABLE … SET SCHEMA</c>). A pure metadata change: rows, indexes, constraints,
    /// owned sequences and triggers move with their table, and nothing is copied. See
    /// docs/data-ownership.md.
    /// </summary>
    public partial class MoveTablesToModuleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "campaigns");

            migrationBuilder.RenameTable(
                name: "reward_tiers",
                schema: "public",
                newName: "reward_tiers",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "reward_tier_reservations",
                schema: "public",
                newName: "reward_tier_reservations",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaigns_outbox_messages",
                schema: "public",
                newName: "campaigns_outbox_messages",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaigns_dead_letter_events",
                schema: "public",
                newName: "campaigns_dead_letter_events",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaigns",
                schema: "public",
                newName: "campaigns",
                newSchema: "campaigns");

            migrationBuilder.RenameTable(
                name: "campaign_ledger_entries",
                schema: "public",
                newName: "campaign_ledger_entries",
                newSchema: "campaigns");

            // Functions do not move with a table. The ledger's trigger functions stay bound to
            // their triggers (by OID), but a PL/pgSQL body resolves table names through
            // search_path at run time, so the unqualified campaign_ledger_entries inside the
            // snapshot check would no longer be found. Move both functions into the module's
            // schema and recreate the snapshot check with schema-qualified names.
            migrationBuilder.Sql(
                """
                ALTER FUNCTION public.campaign_ledger_entries_reject_changes() SET SCHEMA campaigns;
                ALTER FUNCTION public.campaigns_raised_amount_matches_ledger() SET SCHEMA campaigns;

                CREATE OR REPLACE FUNCTION campaigns.campaigns_raised_amount_matches_ledger() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    ledger_total numeric(18,2);
                BEGIN
                    SELECT COALESCE(SUM("Amount"), 0) INTO ledger_total
                    FROM campaigns.campaign_ledger_entries WHERE "CampaignId" = NEW."Id";

                    IF NEW.raised_amount <> ledger_total THEN
                        RAISE EXCEPTION 'campaigns.raised_amount (%) must equal the sum of campaign_ledger_entries (%); append a ledger entry instead', NEW.raised_amount, ledger_total
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION campaigns.campaigns_raised_amount_matches_ledger() RETURNS trigger
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

                ALTER FUNCTION campaigns.campaigns_raised_amount_matches_ledger() SET SCHEMA public;
                ALTER FUNCTION campaigns.campaign_ledger_entries_reject_changes() SET SCHEMA public;
                """);

            migrationBuilder.RenameTable(
                name: "reward_tiers",
                schema: "campaigns",
                newName: "reward_tiers",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "reward_tier_reservations",
                schema: "campaigns",
                newName: "reward_tier_reservations",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaigns_outbox_messages",
                schema: "campaigns",
                newName: "campaigns_outbox_messages",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaigns_dead_letter_events",
                schema: "campaigns",
                newName: "campaigns_dead_letter_events",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaigns",
                schema: "campaigns",
                newName: "campaigns",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "campaign_ledger_entries",
                schema: "campaigns",
                newName: "campaign_ledger_entries",
                newSchema: "public");
        }
    }
}
