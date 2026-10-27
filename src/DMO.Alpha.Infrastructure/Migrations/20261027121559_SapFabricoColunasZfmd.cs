using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SapFabricoColunasZfmd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As outras colunas do relatório da ZFMD (o centro, a cadência, as
            // gotas, os sectores, o KCR, o Δ do peso, o Pack to pull, o Net
            // Quant, as paletes, as rejeições e o HotEnd; Diogo, 27/10/2026).
            // Só acrescenta colunas vazias: não muda dados e a versão anterior
            // da app continua a funcionar com elas. Enchem-se na próxima
            // leitura do dmo-fabrico-sap.txt.
            migrationBuilder.AddColumn<int>(
                name: "Cadencia",
                table: "SapFabricos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Centro",
                table: "SapFabricos",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeltaPesoGramas",
                table: "SapFabricos",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gob",
                table: "SapFabricos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HotEnd",
                table: "SapFabricos",
                type: "numeric(6,3)",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Kcr",
                table: "SapFabricos",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NetQuant",
                table: "SapFabricos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackToPull",
                table: "SapFabricos",
                type: "numeric(6,3)",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Paletes",
                table: "SapFabricos",
                type: "numeric(11,3)",
                precision: 11,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejeicaoMil",
                table: "SapFabricos",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejeicaoTotal",
                table: "SapFabricos",
                type: "numeric(6,3)",
                precision: 6,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sectores",
                table: "SapFabricos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cadencia",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "Centro",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "DeltaPesoGramas",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "Gob",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "HotEnd",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "Kcr",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "NetQuant",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "PackToPull",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "Paletes",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "RejeicaoMil",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "RejeicaoTotal",
                table: "SapFabricos");

            migrationBuilder.DropColumn(
                name: "Sectores",
                table: "SapFabricos");
        }
    }
}
