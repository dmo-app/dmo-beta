using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBqDiscrepancyReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DiscrepancyResetAtUtc",
                table: "BqRepairTraces",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscrepancyResetByNameFrozen",
                table: "BqRepairTraces",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscrepancyResetAtUtc",
                table: "BqRepairTraces");

            migrationBuilder.DropColumn(
                name: "DiscrepancyResetByNameFrozen",
                table: "BqRepairTraces");
        }
    }
}
