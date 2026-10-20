using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArmazemPosicoesEReparadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArmazemPosicoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriadaPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CriadaPorUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmazemPosicoes", x => x.Id);
                    table.CheckConstraint("CK_ArmazemPosicoes_Codigo", "\"Codigo\" ~ '^[0-9A-Z]{1,16}$'");
                    table.ForeignKey(
                        name: "FK_ArmazemPosicoes_Users_CriadaPorUserId",
                        column: x => x.CriadaPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Reparadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AplicaCm = table.Column<bool>(type: "boolean", nullable: false),
                    AplicaMf = table.Column<bool>(type: "boolean", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reparadores", x => x.Id);
                    table.CheckConstraint("CK_Reparadores_Familia", "\"AplicaCm\" OR \"AplicaMf\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemPosicoes_Codigo",
                table: "ArmazemPosicoes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemPosicoes_CriadaPorUserId",
                table: "ArmazemPosicoes",
                column: "CriadaPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparadores_Nome",
                table: "Reparadores",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArmazemPosicoes");

            migrationBuilder.DropTable(
                name: "Reparadores");
        }
    }
}
