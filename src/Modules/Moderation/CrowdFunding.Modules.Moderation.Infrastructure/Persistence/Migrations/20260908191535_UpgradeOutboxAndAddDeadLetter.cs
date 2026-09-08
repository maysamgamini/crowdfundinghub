using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeOutboxAndAddDeadLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_moderation_outbox_messages_ProcessedOnUtc_OccurredOnUtc",
                table: "moderation_outbox_messages");

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "moderation_outbox_messages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "moderation_outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                table: "moderation_outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAtUtc",
                table: "moderation_outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "moderation_outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "moderation_outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "moderation_dead_letter_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_dead_letter_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_outbox_messages_pending",
                table: "moderation_outbox_messages",
                columns: new[] { "ScheduledAtUtc", "Id" },
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_dead_letter_events_SourceEventId",
                table: "moderation_dead_letter_events",
                column: "SourceEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moderation_dead_letter_events");

            migrationBuilder.DropIndex(
                name: "ix_moderation_outbox_messages_pending",
                table: "moderation_outbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "moderation_outbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "moderation_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ScheduledAtUtc",
                table: "moderation_outbox_messages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "moderation_outbox_messages");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "moderation_outbox_messages");

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "moderation_outbox_messages",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.CreateIndex(
                name: "IX_moderation_outbox_messages_ProcessedOnUtc_OccurredOnUtc",
                table: "moderation_outbox_messages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });
        }
    }
}
