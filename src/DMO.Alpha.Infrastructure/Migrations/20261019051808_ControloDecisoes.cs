using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ControloDecisoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControloDecisoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: false),
                    Parte = table.Column<int>(type: "integer", nullable: false),
                    Aprovado = table.Column<bool>(type: "boolean", nullable: false),
                    OperatorId = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Nota = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControloDecisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControloDecisoes_Controlos_ControloId",
                        column: x => x.ControloId,
                        principalTable: "Controlos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControloDecisoes_ControloId_Parte",
                table: "ControloDecisoes",
                columns: new[] { "ControloId", "Parte" },
                unique: true);

            // As decisões já gravadas valem para as duas partes (Peso e
            // Pegamentos): nada do que já foi aprovado volta a
            // ficar pendente (19/10/2026).
            migrationBuilder.Sql("""
                INSERT INTO "ControloDecisoes" ("ControloId", "Parte", "Aprovado", "OperatorId", "EmUtc", "Nota")
                SELECT c."Id", p.parte, c."ApprovedAtUtc" IS NOT NULL,
                       COALESCE(c."ApprovedByOperatorId", c."NotApprovedByOperatorId"),
                       COALESCE(c."ApprovedAtUtc", c."NotApprovedAtUtc"),
                       c."ReviewNote"
                FROM "Controlos" c
                CROSS JOIN (VALUES (1), (2)) AS p(parte)
                WHERE COALESCE(c."ApprovedByOperatorId", c."NotApprovedByOperatorId") IS NOT NULL
                  AND COALESCE(c."ApprovedAtUtc", c."NotApprovedAtUtc") IS NOT NULL
                ON CONFLICT ("ControloId", "Parte") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControloDecisoes");
        }
    }
}
