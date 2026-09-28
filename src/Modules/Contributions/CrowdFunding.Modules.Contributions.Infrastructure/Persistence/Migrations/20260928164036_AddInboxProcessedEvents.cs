using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxProcessedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contributions_processed_events",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConsumerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contributions_processed_events", x => new { x.EventId, x.ConsumerName });
                });

            // Carry the webhook dedupe history into the generic inbox so an event the gateway
            // redelivers after this deploy is still recognized as already processed.
            migrationBuilder.Sql(
                """
                INSERT INTO contributions_processed_events ("EventId", "ConsumerName", "ProcessedAtUtc")
                SELECT "WebhookEventId", 'ReconcilePaymentWebhookCommandHandler', "ProcessedAtUtc"
                FROM processed_payment_webhooks;
                """);

            migrationBuilder.DropTable(
                name: "processed_payment_webhooks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contributions_processed_events");

            migrationBuilder.CreateTable(
                name: "processed_payment_webhooks",
                columns: table => new
                {
                    WebhookEventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PaymentIntentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_payment_webhooks", x => x.WebhookEventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_processed_payment_webhooks_PaymentIntentId",
                table: "processed_payment_webhooks",
                column: "PaymentIntentId");
        }
    }
}
