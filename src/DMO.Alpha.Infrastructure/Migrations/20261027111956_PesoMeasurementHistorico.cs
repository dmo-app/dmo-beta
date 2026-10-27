using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PesoMeasurementHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PesoMeasurementHistorico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PesoId = table.Column<int>(type: "integer", nullable: false),
                    MeasurementId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Apagada = table.Column<bool>(type: "boolean", nullable: false),
                    CmSubjectAntes = table.Column<string>(type: "text", nullable: false),
                    WaterWeightAntesGrams = table.Column<decimal>(type: "numeric", nullable: false),
                    CmSubjectDepois = table.Column<string>(type: "text", nullable: true),
                    WaterWeightDepoisGrams = table.Column<decimal>(type: "numeric", nullable: true),
                    Por = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    PorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesoMeasurementHistorico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PesoMeasurementHistorico_Pesos_PesoId",
                        column: x => x.PesoId,
                        principalTable: "Pesos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PesoMeasurementHistorico_Users_PorUserId",
                        column: x => x.PorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PesoMeasurementHistorico_PesoId_EmUtc",
                table: "PesoMeasurementHistorico",
                columns: new[] { "PesoId", "EmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PesoMeasurementHistorico_PorUserId",
                table: "PesoMeasurementHistorico",
                column: "PorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PesoMeasurementHistorico");
        }
    }
}
