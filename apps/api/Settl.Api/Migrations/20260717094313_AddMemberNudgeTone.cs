using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settl.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberNudgeTone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NudgeTone",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "Direct");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NudgeTone",
                table: "Members");
        }
    }
}
