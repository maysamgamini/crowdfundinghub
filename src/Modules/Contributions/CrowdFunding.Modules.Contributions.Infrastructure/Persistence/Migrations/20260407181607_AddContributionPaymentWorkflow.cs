using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionPaymentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "contributions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "contributions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAtUtc",
                table: "contributions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "contributions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "ProcessedAtUtc",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "contributions");
        }
    }
}
