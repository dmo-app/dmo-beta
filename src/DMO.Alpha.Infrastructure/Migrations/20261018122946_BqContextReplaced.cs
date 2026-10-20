using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BqContextReplaced : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "BqContexts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacedAtUtc",
                table: "BqContexts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacedJobOnId",
                table: "BqContexts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BqContexts_ReplacedJobOnId",
                table: "BqContexts",
                column: "ReplacedJobOnId");

            migrationBuilder.AddForeignKey(
                name: "FK_BqContexts_JobOns_ReplacedJobOnId",
                table: "BqContexts",
                column: "ReplacedJobOnId",
                principalTable: "JobOns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BqContexts_JobOns_ReplacedJobOnId",
                table: "BqContexts");

            migrationBuilder.DropIndex(
                name: "IX_BqContexts_ReplacedJobOnId",
                table: "BqContexts");

            migrationBuilder.DropColumn(
                name: "ReplacedAtUtc",
                table: "BqContexts");

            migrationBuilder.DropColumn(
                name: "ReplacedJobOnId",
                table: "BqContexts");

            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "BqContexts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
