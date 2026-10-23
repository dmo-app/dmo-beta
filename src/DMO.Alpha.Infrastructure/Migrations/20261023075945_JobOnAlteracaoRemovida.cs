using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JobOnAlteracaoRemovida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAtUtc",
                table: "JobOnChanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovedByNameFrozen",
                table: "JobOnChanges",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemovedByUserId",
                table: "JobOnChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOnChanges_RemovedByUserId",
                table: "JobOnChanges",
                column: "RemovedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobOnChanges_Users_RemovedByUserId",
                table: "JobOnChanges",
                column: "RemovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobOnChanges_Users_RemovedByUserId",
                table: "JobOnChanges");

            migrationBuilder.DropIndex(
                name: "IX_JobOnChanges_RemovedByUserId",
                table: "JobOnChanges");

            migrationBuilder.DropColumn(
                name: "RemovedAtUtc",
                table: "JobOnChanges");

            migrationBuilder.DropColumn(
                name: "RemovedByNameFrozen",
                table: "JobOnChanges");

            migrationBuilder.DropColumn(
                name: "RemovedByUserId",
                table: "JobOnChanges");
        }
    }
}
