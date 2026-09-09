using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.BuildingBlocks.Infrastructure.Audit.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "system");

            migrationBuilder.CreateTable(
                name: "audit_records",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    command_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_actor_id",
                schema: "system",
                table: "audit_records",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_timestamp_utc",
                schema: "system",
                table: "audit_records",
                column: "timestamp_utc");

            // TICKET-039 acceptance criterion 3: explicit, defense-in-depth append-only
            // enforcement. PostgreSQL already grants no privileges to PUBLIC on a new table by
            // default, but stating it explicitly documents the intent and holds even if that
            // default is ever changed at the database/role level. This has no effect on the
            // table owner (whichever role runs migrations) or a superuser — a production
            // deployment must have its application connect as a distinct, non-owner role granted
            // only SELECT/INSERT for this guarantee to actually bind the running app (see
            // AuditRecordAppendOnlyE2ETests, which provisions exactly such a role to prove the
            // mechanism holds).
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON system.audit_records FROM PUBLIC;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records",
                schema: "system");
        }
    }
}
