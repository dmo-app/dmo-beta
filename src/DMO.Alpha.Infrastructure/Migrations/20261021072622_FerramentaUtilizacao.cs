using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FerramentaUtilizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FerramentaUtilizacoes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Percentagem = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    Origem = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RegistadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    RegistadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CodigoEquipamento = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Consumo = table.Column<long>(type: "bigint", nullable: true),
                    VidaPrevista = table.Column<long>(type: "bigint", nullable: true),
                    Ficheiro = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    FicheiroModificadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FerramentaUtilizacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FerramentaUtilizacoes_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FerramentaUtilizacoes_Users_RegistadoPorUserId",
                        column: x => x.RegistadoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FerramentaLembretesUtilizacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MovimentoId = table.Column<int>(type: "integer", nullable: true),
                    CriadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CriadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UtilizacaoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FerramentaLembretesUtilizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FerramentaLembretesUtilizacao_ArmazemMovimentos_MovimentoId",
                        column: x => x.MovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FerramentaLembretesUtilizacao_FerramentaUtilizacoes_Utiliza~",
                        column: x => x.UtilizacaoId,
                        principalTable: "FerramentaUtilizacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FerramentaLembretesUtilizacao_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FerramentaLembretesUtilizacao_Users_CriadoPorUserId",
                        column: x => x.CriadoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaLembretesUtilizacao_CriadoPorUserId",
                table: "FerramentaLembretesUtilizacao",
                column: "CriadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaLembretesUtilizacao_MovimentoId",
                table: "FerramentaLembretesUtilizacao",
                column: "MovimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaLembretesUtilizacao_ToolId_Aberto",
                table: "FerramentaLembretesUtilizacao",
                column: "ToolId",
                unique: true,
                filter: "\"FechadoEmUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaLembretesUtilizacao_UtilizacaoId",
                table: "FerramentaLembretesUtilizacao",
                column: "UtilizacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaUtilizacoes_RegistadoPorUserId",
                table: "FerramentaUtilizacoes",
                column: "RegistadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaUtilizacoes_ToolId_Id",
                table: "FerramentaUtilizacoes",
                columns: new[] { "ToolId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FerramentaLembretesUtilizacao");

            migrationBuilder.DropTable(
                name: "FerramentaUtilizacoes");
        }
    }
}
