using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settl.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringEndDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "RecurringTemplates",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "RecurringTemplates");
        }
    }
}
