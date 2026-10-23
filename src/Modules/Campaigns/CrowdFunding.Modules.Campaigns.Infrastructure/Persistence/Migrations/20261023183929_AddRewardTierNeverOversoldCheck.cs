using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardTierNeverOversoldCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_reward_tiers_never_oversold",
                schema: "campaigns",
                table: "reward_tiers",
                sql: "\"ClaimedCount\" >= 0 AND \"ReservedCount\" >= 0 AND \"ClaimedCount\" + \"ReservedCount\" <= \"TotalCapacity\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_reward_tiers_never_oversold",
                schema: "campaigns",
                table: "reward_tiers");
        }
    }
}
