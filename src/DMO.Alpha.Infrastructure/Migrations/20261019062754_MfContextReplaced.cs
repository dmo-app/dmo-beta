using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MfContextReplaced : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "MfContexts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacedAtUtc",
                table: "MfContexts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacedJobOnId",
                table: "MfContexts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfContexts_ReplacedJobOnId",
                table: "MfContexts",
                column: "ReplacedJobOnId");

            migrationBuilder.AddForeignKey(
                name: "FK_MfContexts_JobOns_ReplacedJobOnId",
                table: "MfContexts",
                column: "ReplacedJobOnId",
                principalTable: "JobOns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MfContexts_JobOns_ReplacedJobOnId",
                table: "MfContexts");

            migrationBuilder.DropIndex(
                name: "IX_MfContexts_ReplacedJobOnId",
                table: "MfContexts");

            migrationBuilder.DropColumn(
                name: "ReplacedAtUtc",
                table: "MfContexts");

            migrationBuilder.DropColumn(
                name: "ReplacedJobOnId",
                table: "MfContexts");

            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "MfContexts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
