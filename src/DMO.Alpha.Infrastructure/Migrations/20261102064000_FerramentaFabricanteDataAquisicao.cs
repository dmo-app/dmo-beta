using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>R6 (Diogo, 02/11/2026): fabricante do molde e data de aquisição escritos à mão quando o SAP não os tem.</summary>
    public partial class FerramentaFabricanteDataAquisicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fabricante",
                table: "Tools",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataAquisicao",
                table: "Tools",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Fabricante", table: "Tools");
            migrationBuilder.DropColumn(name: "DataAquisicao", table: "Tools");
        }
    }
}
