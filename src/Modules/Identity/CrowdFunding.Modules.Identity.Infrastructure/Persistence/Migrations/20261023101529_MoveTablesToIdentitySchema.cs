using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToIdentitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "users",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "user_roles",
                newName: "user_roles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "user_permissions",
                newName: "user_permissions",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                newName: "refresh_tokens",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "identity_signing_keys",
                newName: "identity_signing_keys",
                newSchema: "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "users",
                schema: "identity",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "user_roles",
                schema: "identity",
                newName: "user_roles");

            migrationBuilder.RenameTable(
                name: "user_permissions",
                schema: "identity",
                newName: "user_permissions");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                schema: "identity",
                newName: "refresh_tokens");

            migrationBuilder.RenameTable(
                name: "identity_signing_keys",
                schema: "identity",
                newName: "identity_signing_keys");
        }
    }
}
