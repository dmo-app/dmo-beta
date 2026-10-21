using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SapFabricoPeso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasProducao",
                table: "SapFabricos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoMedioGramas",
                table: "SapFabricos",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasProducao",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "PesoMedioGramas",
                table: "SapFabricos");
        }
    }
}
