using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPledgeActivations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pledge_activations",
                schema: "contributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethodReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pledge_activations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pledge_activations_contributions_ContributionId",
                        column: x => x.ContributionId,
                        principalSchema: "contributions",
                        principalTable: "contributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_pledge_activations_one_per_pledge",
                schema: "contributions",
                table: "pledge_activations",
                column: "ContributionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pledge_activations",
                schema: "contributions");
        }
    }
}
