using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settl.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdOwnershipAndArchival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Households",
                type: "timestamp with time zone",
                nullable: true);

            // Add nullable first so existing rows aren't rejected, then backfill the owner to
            // each household's earliest-JoinedAt member (tie-break by MemberId — the same order
            // MembershipOrder uses), then enforce NOT NULL. ADR-0016.
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerMemberId",
                table: "Households",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Households" h
                SET "OwnerMemberId" = sub."MemberId"
                FROM (
                    SELECT DISTINCT ON ("HouseholdId") "HouseholdId", "MemberId"
                    FROM "HouseholdMemberships"
                    ORDER BY "HouseholdId", "JoinedAt", "MemberId"
                ) sub
                WHERE h."Id" = sub."HouseholdId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerMemberId",
                table: "Households",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "OwnerMemberId",
                table: "Households");
        }
    }
}
