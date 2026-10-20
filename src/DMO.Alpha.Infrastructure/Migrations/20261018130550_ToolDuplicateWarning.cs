using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ToolDuplicateWarning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DuplicateWarningAtUtc",
                table: "Tools",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DuplicateWarningByNameFrozen",
                table: "Tools",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegisteredAtUtc",
                table: "Tools",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuplicateWarningAtUtc",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "DuplicateWarningByNameFrozen",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "RegisteredAtUtc",
                table: "Tools");
        }
    }
}
