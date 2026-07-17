using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settl.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIouEntryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive backfill (ADR-0021): convert any surviving IOU rows into their
            // balance-equivalent "Allt på en" amount-split expense BEFORE the columns drop,
            // so a stray row can't silently lose its debt. The debtor (FromMemberId) owes the
            // whole amount; the creditor (ToMemberId) becomes the payer. Type/SplitMode are
            // stored as strings (SettlDbContext HasConversion<string>). No-op when none exist.
            migrationBuilder.Sql(
                """
                INSERT INTO "EntryShares" ("EntryId", "MemberId", "ShareMinor", "FormulaValue")
                SELECT "Id", "FromMemberId", "AmountMinor", "AmountMinor"
                FROM "Entries"
                WHERE "Type" = 'Iou' AND "FromMemberId" IS NOT NULL;
                """);
            migrationBuilder.Sql(
                """
                UPDATE "Entries"
                SET "Type" = 'Expense', "PaidByMemberId" = "ToMemberId", "SplitMode" = 'Amount'
                WHERE "Type" = 'Iou';
                """);

            migrationBuilder.DropColumn(
                name: "FromMemberId",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "ToMemberId",
                table: "Entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FromMemberId",
                table: "Entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToMemberId",
                table: "Entries",
                type: "uuid",
                nullable: true);
        }
    }
}
