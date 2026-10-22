using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReparacaoProgramada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReparacaoProgramadaListas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReparadorId = table.Column<int>(type: "integer", nullable: true),
                    ReparadorNome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CriadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriadaPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CriadaPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoProgramadaListas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoProgramadaListas_Reparadores_ReparadorId",
                        column: x => x.ReparadorId,
                        principalTable: "Reparadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacaoProgramadaListas_Users_CriadaPorUserId",
                        column: x => x.CriadaPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoProgramadaItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListaId = table.Column<int>(type: "integer", nullable: false),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdicionadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Referencia = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Lote = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Utilizacao = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    Linhas = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SaidaMovimentoId = table.Column<int>(type: "integer", nullable: true),
                    EntradaMovimentoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoProgramadaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoProgramadaItens_ArmazemMovimentos_EntradaMovimento~",
                        column: x => x.EntradaMovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacaoProgramadaItens_ArmazemMovimentos_SaidaMovimentoId",
                        column: x => x.SaidaMovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacaoProgramadaItens_ReparacaoProgramadaListas_ListaId",
                        column: x => x.ListaId,
                        principalTable: "ReparacaoProgramadaListas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReparacaoProgramadaItens_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoProgramadaItens_EntradaMovimentoId",
                table: "ReparacaoProgramadaItens",
                column: "EntradaMovimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoProgramadaItens_ListaId_ToolId",
                table: "ReparacaoProgramadaItens",
                columns: new[] { "ListaId", "ToolId" },
                unique: true,
                filter: "\"ToolId\" <> 'REMOVED'");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoProgramadaItens_SaidaMovimentoId",
                table: "ReparacaoProgramadaItens",
                column: "SaidaMovimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoProgramadaItens_ToolId",
                table: "ReparacaoProgramadaItens",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoProgramadaListas_CriadaPorUserId",
                table: "ReparacaoProgramadaListas",
                column: "CriadaPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoProgramadaListas_FechadaEmUtc",
                table: "ReparacaoProgramadaListas",
                column: "FechadaEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoProgramadaListas_ReparadorId",
                table: "ReparacaoProgramadaListas",
                column: "ReparadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReparacaoProgramadaItens");

            migrationBuilder.DropTable(
                name: "ReparacaoProgramadaListas");
        }
    }
}
