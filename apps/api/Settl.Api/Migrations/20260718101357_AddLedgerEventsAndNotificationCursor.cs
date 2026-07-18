using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settl.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerEventsAndNotificationCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NotificationsSeenAt",
                table: "Members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LedgerEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecurringTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedMemberIdsCsv = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerEvents_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEvent_Household_OccurredAt",
                table: "LedgerEvents",
                columns: new[] { "HouseholdId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LedgerEvents");

            migrationBuilder.DropColumn(
                name: "NotificationsSeenAt",
                table: "Members");
        }
    }
}
