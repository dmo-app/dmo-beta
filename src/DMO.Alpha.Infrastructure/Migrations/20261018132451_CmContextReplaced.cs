using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CmContextReplaced : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "CmContexts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacedAtUtc",
                table: "CmContexts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplacedJobOnId",
                table: "CmContexts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmContexts_ReplacedJobOnId",
                table: "CmContexts",
                column: "ReplacedJobOnId");

            migrationBuilder.AddForeignKey(
                name: "FK_CmContexts_JobOns_ReplacedJobOnId",
                table: "CmContexts",
                column: "ReplacedJobOnId",
                principalTable: "JobOns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CmContexts_JobOns_ReplacedJobOnId",
                table: "CmContexts");

            migrationBuilder.DropIndex(
                name: "IX_CmContexts_ReplacedJobOnId",
                table: "CmContexts");

            migrationBuilder.DropColumn(
                name: "ReplacedAtUtc",
                table: "CmContexts");

            migrationBuilder.DropColumn(
                name: "ReplacedJobOnId",
                table: "CmContexts");

            migrationBuilder.AlterColumn<int>(
                name: "JobOnId",
                table: "CmContexts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
