using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PegamentosRastoDasLeituras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PegamentosMeasurementHistorico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PegamentosId = table.Column<int>(type: "integer", nullable: false),
                    MeasurementId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Component = table.Column<string>(type: "text", nullable: false),
                    Apagada = table.Column<bool>(type: "boolean", nullable: false),
                    CosturaAntesMm = table.Column<decimal>(type: "numeric", nullable: false),
                    ContraCosturaAntesMm = table.Column<decimal>(type: "numeric", nullable: true),
                    NominalGargaloAntesMm = table.Column<decimal>(type: "numeric", nullable: true),
                    CosturaDepoisMm = table.Column<decimal>(type: "numeric", nullable: true),
                    ContraCosturaDepoisMm = table.Column<decimal>(type: "numeric", nullable: true),
                    NominalGargaloDepoisMm = table.Column<decimal>(type: "numeric", nullable: true),
                    Por = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    EmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PegamentosMeasurementHistorico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PegamentosMeasurementHistorico_Pegamentos_PegamentosId",
                        column: x => x.PegamentosId,
                        principalTable: "Pegamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PegamentosMeasurementHistorico_PegamentosId_EmUtc",
                table: "PegamentosMeasurementHistorico",
                columns: new[] { "PegamentosId", "EmUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PegamentosMeasurementHistorico");
        }
    }
}
