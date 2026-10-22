using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArmazemLotesNovos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArmazemLotesNovos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Referencia = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Lote = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Posicao = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    RegistadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    RegistadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedToolId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MovimentoId = table.Column<int>(type: "integer", nullable: true),
                    ResolvidoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvidoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ResolvidoPorUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmazemLotesNovos", x => x.Id);
                    table.CheckConstraint("CK_ArmazemLotesNovos_Posicao", "\"Posicao\" ~ '^[0-9]{4}$'");
                    table.ForeignKey(
                        name: "FK_ArmazemLotesNovos_ArmazemMovimentos_MovimentoId",
                        column: x => x.MovimentoId,
                        principalTable: "ArmazemMovimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ArmazemLotesNovos_Tools_ResolvedToolId",
                        column: x => x.ResolvedToolId,
                        principalTable: "Tools",
                        principalColumn: "ToolId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ArmazemLotesNovos_Users_RegistadoPorUserId",
                        column: x => x.RegistadoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ArmazemLotesNovos_Users_ResolvidoPorUserId",
                        column: x => x.ResolvidoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemLotesNovos_Estado_Tipo",
                table: "ArmazemLotesNovos",
                columns: new[] { "Estado", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemLotesNovos_MovimentoId",
                table: "ArmazemLotesNovos",
                column: "MovimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemLotesNovos_RegistadoPorUserId",
                table: "ArmazemLotesNovos",
                column: "RegistadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemLotesNovos_ResolvedToolId",
                table: "ArmazemLotesNovos",
                column: "ResolvedToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemLotesNovos_ResolvidoPorUserId",
                table: "ArmazemLotesNovos",
                column: "ResolvidoPorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArmazemLotesNovos");
        }
    }
}
