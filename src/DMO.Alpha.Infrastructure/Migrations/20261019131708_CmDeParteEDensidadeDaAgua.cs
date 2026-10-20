using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CmDeParteEDensidadeDaAgua : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WaterDensityGramsPerCm3",
                table: "Pesos",
                type: "numeric",
                nullable: true);

            // AV-PESO-01 (19/10/2026): cada Peso guarda a densidade da água que
            // usou. Os Pesos que já existem ficam com a da tabela com que
            // foram gravados: a da folha «Temp» antes da DT9 (beta.283, junta
            // a 2026-10-19 12:09:31 UTC; só 6, 18 e 30 °C mudaram) ou, nos
            // gravados depois, a da DT9. Só preenche a coluna nova.
            migrationBuilder.Sql("""
                UPDATE "Pesos" AS p
                SET "WaterDensityGramsPerCm3" =
                    CASE WHEN p."RegistadoEmUtc" IS NULL OR p."RegistadoEmUtc" < TIMESTAMPTZ '2026-10-19 12:09:31+00'
                         THEN t.antes ELSE t.dt9 END
                FROM (VALUES
                    (5, 0.99888, 0.99888),
                    (6, 0.99885, 0.99886),
                    (7, 0.99882, 0.99882),
                    (8, 0.99877, 0.99877),
                    (9, 0.99871, 0.99871),
                    (10, 0.99863, 0.99863),
                    (11, 0.99854, 0.99854),
                    (12, 0.99844, 0.99844),
                    (13, 0.99832, 0.99832),
                    (14, 0.99819, 0.99819),
                    (15, 0.99805, 0.99805),
                    (16, 0.99789, 0.99789),
                    (17, 0.99773, 0.99773),
                    (18, 0.99765, 0.99755),
                    (19, 0.99737, 0.99737),
                    (20, 0.99717, 0.99717),
                    (21, 0.99696, 0.99696),
                    (22, 0.99674, 0.99674),
                    (23, 0.99652, 0.99652),
                    (24, 0.99628, 0.99628),
                    (25, 0.99603, 0.99603),
                    (26, 0.99577, 0.99577),
                    (27, 0.99551, 0.99551),
                    (28, 0.99523, 0.99523),
                    (29, 0.99494, 0.99494),
                    (30, 0.99485, 0.99465),
                    (31, 0.99435, 0.99435),
                    (32, 0.99403, 0.99403),
                    (33, 0.99371, 0.99371),
                    (34, 0.99339, 0.99339),
                    (35, 0.99305, 0.99305)
                ) AS t(grau, antes, dt9)
                WHERE p."WaterTemperatureC" = t.grau
                  AND p."WaterDensityGramsPerCm3" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "PesoComparacaoCmDecisoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeasurementId = table.Column<int>(type: "integer", nullable: false),
                    DeParte = table.Column<bool>(type: "boolean", nullable: false),
                    EscolhidoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EscolhidoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Aprovado = table.Column<bool>(type: "boolean", nullable: true),
                    AprovadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    AprovadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesoComparacaoCmDecisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PesoComparacaoCmDecisoes_PesoComparacaoMeasurements_Measure~",
                        column: x => x.MeasurementId,
                        principalTable: "PesoComparacaoMeasurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PesoComparacaoCmDecisoes_MeasurementId",
                table: "PesoComparacaoCmDecisoes",
                column: "MeasurementId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PesoComparacaoCmDecisoes");

            migrationBuilder.DropColumn(
                name: "WaterDensityGramsPerCm3",
                table: "Pesos");
        }
    }
}
