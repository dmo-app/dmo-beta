using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AvisosSapPecasFornecedorData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SapAvisosAlteradosEmUtc",
                table: "Tools",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SapDataAquisicao",
                table: "Tools",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SapFornecedor",
                table: "Tools",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SapPecas",
                table: "Tools",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SapPecasIgnoradas",
                table: "Tools",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SapPercentagem",
                table: "Tools",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SapPercentagemIgnorada",
                table: "Tools",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SapAvisosAlteradosEmUtc",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "SapDataAquisicao",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "SapFornecedor",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "SapPecas",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "SapPecasIgnoradas",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "SapPercentagem",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "SapPercentagemIgnorada",
                table: "Tools");
        }
    }
}
