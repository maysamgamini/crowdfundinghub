using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundThroughProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastRefundFailure",
                schema: "pledges",
                table: "pledges",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundFailures",
                schema: "pledges",
                table: "pledges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                schema: "pledges",
                table: "pledges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRefundFailure",
                schema: "pledges",
                table: "pledges");

            migrationBuilder.DropColumn(
                name: "RefundFailures",
                schema: "pledges",
                table: "pledges");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                schema: "pledges",
                table: "pledges");
        }
    }
}
