using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPledgeActivations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pledge_activations",
                schema: "pledges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PledgeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethodReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pledge_activations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pledge_activations_pledges_PledgeId",
                        column: x => x.PledgeId,
                        principalSchema: "pledges",
                        principalTable: "pledges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_pledge_activations_one_per_pledge",
                schema: "pledges",
                table: "pledge_activations",
                column: "PledgeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pledge_activations",
                schema: "pledges");
        }
    }
}
