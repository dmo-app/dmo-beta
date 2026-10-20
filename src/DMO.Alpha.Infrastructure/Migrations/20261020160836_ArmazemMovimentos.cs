using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArmazemMovimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArmazemMovimentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ToolReferenceFrozen = table.Column<string>(type: "text", nullable: true),
                    ToolLotFrozen = table.Column<string>(type: "text", nullable: true),
                    PosicaoId = table.Column<int>(type: "integer", nullable: true),
                    PosicaoAnteriorId = table.Column<int>(type: "integer", nullable: true),
                    Origem = table.Column<int>(type: "integer", nullable: true),
                    Destino = table.Column<int>(type: "integer", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: true),
                    ReparadorId = table.Column<int>(type: "integer", nullable: true),
                    BqReparadorId = table.Column<int>(type: "integer", nullable: true),
                    ReparadorNome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FabricaDestino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OcorridoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    RegistadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersaoApp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmazemMovimentos", x => x.Id);
                    table.CheckConstraint("CK_ArmazemMovimentos_UmReparador", "\"ReparadorId\" IS NULL OR \"BqReparadorId\" IS NULL");
                    table.ForeignKey(
                        name: "FK_ArmazemMovimentos_ArmazemPosicoes_PosicaoAnteriorId",
                        column: x => x.PosicaoAnteriorId,
                        principalTable: "ArmazemPosicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemMovimentos_ArmazemPosicoes_PosicaoId",
                        column: x => x.PosicaoId,
                        principalTable: "ArmazemPosicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemMovimentos_BqRepairers_BqReparadorId",
                        column: x => x.BqReparadorId,
                        principalTable: "BqRepairers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemMovimentos_Reparadores_ReparadorId",
                        column: x => x.ReparadorId,
                        principalTable: "Reparadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemMovimentos_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemMovimentos_Users_RegistadoPorUserId",
                        column: x => x.RegistadoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ArmazemOcupacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PosicaoId = table.Column<int>(type: "integer", nullable: false),
                    MovimentoId = table.Column<int>(type: "integer", nullable: false),
                    DesdeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LibertadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LibertadaPorMovimentoId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmazemOcupacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArmazemOcupacoes_ArmazemMovimentos_LibertadaPorMovimentoId",
                        column: x => x.LibertadaPorMovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemOcupacoes_ArmazemMovimentos_MovimentoId",
                        column: x => x.MovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemOcupacoes_ArmazemPosicoes_PosicaoId",
                        column: x => x.PosicaoId,
                        principalTable: "ArmazemPosicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArmazemOcupacoes_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FerramentaEstados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Origem = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MovimentoId = table.Column<int>(type: "integer", nullable: true),
                    RegistadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    RegistadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FerramentaEstados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FerramentaEstados_ArmazemMovimentos_MovimentoId",
                        column: x => x.MovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FerramentaEstados_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FerramentaEstados_Users_RegistadoPorUserId",
                        column: x => x.RegistadoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FerramentaLocalizacoes",
                columns: table => new
                {
                    ToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Localizacao = table.Column<int>(type: "integer", nullable: false),
                    ReparadorNome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DesdeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MovimentoId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FerramentaLocalizacoes", x => x.ToolId);
                    table.ForeignKey(
                        name: "FK_FerramentaLocalizacoes_ArmazemMovimentos_MovimentoId",
                        column: x => x.MovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FerramentaLocalizacoes_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_BqReparadorId",
                table: "ArmazemMovimentos",
                column: "BqReparadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_OcorridoEmUtc",
                table: "ArmazemMovimentos",
                column: "OcorridoEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_OperacaoId",
                table: "ArmazemMovimentos",
                column: "OperacaoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_PosicaoAnteriorId",
                table: "ArmazemMovimentos",
                column: "PosicaoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_PosicaoId",
                table: "ArmazemMovimentos",
                column: "PosicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_RegistadoPorUserId",
                table: "ArmazemMovimentos",
                column: "RegistadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_ReparadorId",
                table: "ArmazemMovimentos",
                column: "ReparadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemMovimentos_ToolId",
                table: "ArmazemMovimentos",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemOcupacoes_LibertadaPorMovimentoId",
                table: "ArmazemOcupacoes",
                column: "LibertadaPorMovimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemOcupacoes_MovimentoId",
                table: "ArmazemOcupacoes",
                column: "MovimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemOcupacoes_PosicaoId",
                table: "ArmazemOcupacoes",
                column: "PosicaoId",
                filter: "\"LibertadaEmUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemOcupacoes_ToolId_Ativa",
                table: "ArmazemOcupacoes",
                column: "ToolId",
                unique: true,
                filter: "\"LibertadaEmUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaEstados_MovimentoId",
                table: "FerramentaEstados",
                column: "MovimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaEstados_RegistadoPorUserId",
                table: "FerramentaEstados",
                column: "RegistadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaEstados_ToolId_Id",
                table: "FerramentaEstados",
                columns: new[] { "ToolId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FerramentaLocalizacoes_MovimentoId",
                table: "FerramentaLocalizacoes",
                column: "MovimentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArmazemOcupacoes");

            migrationBuilder.DropTable(
                name: "FerramentaEstados");

            migrationBuilder.DropTable(
                name: "FerramentaLocalizacoes");

            migrationBuilder.DropTable(
                name: "ArmazemMovimentos");
        }
    }
}
