using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "contributions",
                table: "contributions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_contributions_ContributorId_IdempotencyKey",
                schema: "contributions",
                table: "contributions",
                columns: new[] { "ContributorId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_contributions_ContributorId_IdempotencyKey",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "contributions",
                table: "contributions");
        }
    }
}
