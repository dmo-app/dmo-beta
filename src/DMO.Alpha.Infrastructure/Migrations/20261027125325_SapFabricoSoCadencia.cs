using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// Das colunas da ZFMD acrescentadas na 20261027121559_SapFabricoColunasZfmd
    /// fica só a cadência (Diogo, 27/10/2026: «podemos guardar cadência mas acho
    /// que o resto não vale a pena»). As outras 11 só tinham o que se lê do SAP
    /// e nunca chegaram à oficial nem ao ensaio; um «Ler pasta» volta a lê-las se
    /// um dia fizerem falta.
    /// </summary>
    public partial class SapFabricoSoCadencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
