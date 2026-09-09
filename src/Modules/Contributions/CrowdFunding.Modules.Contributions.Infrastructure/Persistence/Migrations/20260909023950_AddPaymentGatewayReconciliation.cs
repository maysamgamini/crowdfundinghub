using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewayReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalPaymentIntentId",
                table: "contributions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentGateway",
                table: "contributions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

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
                name: "IX_contributions_ExternalPaymentIntentId",
                table: "contributions",
                column: "ExternalPaymentIntentId",
                unique: true,
                filter: "\"ExternalPaymentIntentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_processed_payment_webhooks_PaymentIntentId",
                table: "processed_payment_webhooks",
                column: "PaymentIntentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_payment_webhooks");

            migrationBuilder.DropIndex(
                name: "IX_contributions_ExternalPaymentIntentId",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "ExternalPaymentIntentId",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "PaymentGateway",
                table: "contributions");
        }
    }
}
