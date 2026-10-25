using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPledgeIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "pledges",
                table: "pledges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_pledges_BackerId_IdempotencyKey",
                schema: "pledges",
                table: "pledges",
                columns: new[] { "BackerId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_pledges_BackerId_IdempotencyKey",
                schema: "pledges",
                table: "pledges");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "pledges",
                table: "pledges");
        }
    }
}
