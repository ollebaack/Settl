using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settl.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false, defaultValue: "SEK"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AvatarColor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Cadence = table.Column<string>(type: "text", nullable: false),
                    NextPostDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidByMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SplitMode = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringTemplates_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InitiatedByMemberId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settlements_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdMemberships",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMemberships", x => new { x.HouseholdId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_HouseholdMemberships_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdMemberships_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    SplitMode = table.Column<string>(type: "text", nullable: false),
                    RecurringTemplateId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entries_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Entries_RecurringTemplates_RecurringTemplateId",
                        column: x => x.RecurringTemplateId,
                        principalTable: "RecurringTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecurringShares",
                columns: table => new
                {
                    RecurringTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormulaValue = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringShares", x => new { x.RecurringTemplateId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_RecurringShares_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringShares_RecurringTemplates_RecurringTemplateId",
                        column: x => x.RecurringTemplateId,
                        principalTable: "RecurringTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntryShares",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareMinor = table.Column<long>(type: "bigint", nullable: false),
                    FormulaValue = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryShares", x => new { x.EntryId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_EntryShares_Entries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntryShares_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SettlementClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DebtorMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditorMemberId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementClosures_Entries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SettlementClosures_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entries_HouseholdId",
                table: "Entries",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_Entry_RecurringTemplate_Date",
                table: "Entries",
                columns: new[] { "RecurringTemplateId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryShares_MemberId",
                table: "EntryShares",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberships_MemberId",
                table: "HouseholdMemberships",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringShares_MemberId",
                table: "RecurringShares",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTemplates_HouseholdId",
                table: "RecurringTemplates",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementClosure_Entry_Debtor_Creditor",
                table: "SettlementClosures",
                columns: new[] { "EntryId", "DebtorMemberId", "CreditorMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementClosures_SettlementId",
                table: "SettlementClosures",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_HouseholdId",
                table: "Settlements",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryShares");

            migrationBuilder.DropTable(
                name: "HouseholdMemberships");

            migrationBuilder.DropTable(
                name: "RecurringShares");

            migrationBuilder.DropTable(
                name: "SettlementClosures");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Entries");

            migrationBuilder.DropTable(
                name: "Settlements");

            migrationBuilder.DropTable(
                name: "RecurringTemplates");

            migrationBuilder.DropTable(
                name: "Households");
        }
    }
}
