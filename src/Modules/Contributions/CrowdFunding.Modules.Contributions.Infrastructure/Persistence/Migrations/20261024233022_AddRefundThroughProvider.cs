using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundThroughProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastRefundFailure",
                schema: "contributions",
                table: "contributions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundFailures",
                schema: "contributions",
                table: "contributions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                schema: "contributions",
                table: "contributions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRefundFailure",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "RefundFailures",
                schema: "contributions",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                schema: "contributions",
                table: "contributions");
        }
    }
}
