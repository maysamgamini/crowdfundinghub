using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundJobsAndAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextRefundAtUtc",
                schema: "pledges",
                table: "pledges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "refund_attempts",
                schema: "pledges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PledgeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderRefundId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_attempts", x => x.Id);
                    table.CheckConstraint("CK_refund_attempts_failure_code_matches_outcome", "(\"Outcome\" = 'Failed' AND \"FailureCode\" IS NOT NULL) OR (\"Outcome\" = 'Succeeded' AND \"FailureCode\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_refund_attempts_pledges_PledgeId",
                        column: x => x.PledgeId,
                        principalSchema: "pledges",
                        principalTable: "pledges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refund_jobs",
                schema: "pledges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                    // NOTE: the xmin concurrency column is deliberately not created: xmin is a PostgreSQL system column that every table already has.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_refund_attempts_pledge_attempt",
                schema: "pledges",
                table: "refund_attempts",
                columns: new[] { "PledgeId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_jobs_open",
                schema: "pledges",
                table: "refund_jobs",
                column: "CreatedAtUtc",
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "UX_refund_jobs_one_per_campaign",
                schema: "pledges",
                table: "refund_jobs",
                column: "CampaignId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refund_attempts",
                schema: "pledges");

            migrationBuilder.DropTable(
                name: "refund_jobs",
                schema: "pledges");

            migrationBuilder.DropColumn(
                name: "NextRefundAtUtc",
                schema: "pledges",
                table: "pledges");
        }
    }
}
