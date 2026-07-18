using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settl.Api.Migrations
{
    /// <inheritdoc />
    public partial class MergeSwishNumberIntoPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // contacts-phone-sms spec: collapse the two numbers into the single inherited PhoneNumber. Where a
            // member saved a Swish number it WINS — it drives a live payment feature, so a
            // wrong number would send money to a stranger. Members with only a profile phone keep
            // it. Both columns are already normalised E.164, so the copy is verbatim.
            migrationBuilder.Sql(
                "UPDATE \"Members\" SET \"PhoneNumber\" = \"SwishNumber\" " +
                "WHERE \"SwishNumber\" IS NOT NULL AND \"SwishNumber\" <> '';");

            migrationBuilder.DropColumn(
                name: "SwishNumber",
                table: "Members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SwishNumber",
                table: "Members",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
