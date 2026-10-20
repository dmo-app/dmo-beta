using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// A Revisão sabe o que chegou depois da aprovação (Diogo, 19/10/2026):
    /// a data de cada leitura nova do Peso e dos Pegamentos e a da última
    /// correção de uma folha («Registado/Corrigido depois da aprovação», C9;
    /// o Peso novo e a folha nova com aprovação própria, C7), e o histórico
    /// do OK/NOK de cada área do controlo (D13). Só acrescenta: as leituras
    /// antigas ficam sem data (não se inventa) e nada muda nos dados.
    /// </summary>
    public partial class ControloRevisaoDepoisDaAprovacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegistadoEmUtc",
                table: "PesoMeasurements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistadoEmUtc",
                table: "PegamentosMeasurements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrigidoEmUtc",
                table: "Pegamentos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ControloAreaStateHistorico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: false),
                    Area = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Por = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    EmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControloAreaStateHistorico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControloAreaStateHistorico_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControloAreaStateHistorico_ControloId_Area_EmUtc",
                table: "ControloAreaStateHistorico",
                columns: new[] { "ControloId", "Area", "EmUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControloAreaStateHistorico");

            migrationBuilder.DropColumn(
                name: "RegistadoEmUtc",
                table: "PesoMeasurements");

            migrationBuilder.DropColumn(
                name: "RegistadoEmUtc",
                table: "PegamentosMeasurements");

            migrationBuilder.DropColumn(
                name: "CorrigidoEmUtc",
                table: "Pegamentos");
        }
    }
}
