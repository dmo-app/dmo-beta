using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MovimentosLinhaReparadorJobOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntradaReparadorNome",
                table: "ArmazemMovimentos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JobOnId",
                table: "ArmazemMovimentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MachineId",
                table: "ArmazemMovimentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_JobOnId",
                table: "ArmazemMovimentos",
                column: "JobOnId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_MachineId",
                table: "ArmazemMovimentos",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArmazemMovimentos_JobOns_JobOnId",
                table: "ArmazemMovimentos",
                column: "JobOnId",
                principalTable: "JobOns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ArmazemMovimentos_Machines_MachineId",
                table: "ArmazemMovimentos",
                column: "MachineId",
                principalTable: "Machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArmazemMovimentos_JobOns_JobOnId",
                table: "ArmazemMovimentos");

            migrationBuilder.DropForeignKey(
                name: "FK_ArmazemMovimentos_Machines_MachineId",
                table: "ArmazemMovimentos");

            migrationBuilder.DropIndex(
                name: "IX_ArmazemMovimentos_JobOnId",
                table: "ArmazemMovimentos");

            migrationBuilder.DropIndex(
                name: "IX_ArmazemMovimentos_MachineId",
                table: "ArmazemMovimentos");

            migrationBuilder.DropColumn(
                name: "EntradaReparadorNome",
                table: "ArmazemMovimentos");

            migrationBuilder.DropColumn(
                name: "JobOnId",
                table: "ArmazemMovimentos");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "ArmazemMovimentos");
        }
    }
}
