using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnvioEmailPeso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailEnvios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ControloId = table.Column<int>(type: "integer", nullable: true),
                    PesoId = table.Column<int>(type: "integer", nullable: false),
                    Ficheiro = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ModeloNome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ListaNome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Remetente = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Destinatarios = table.Column<string>(type: "character varying(13000)", maxLength: 13000, nullable: false),
                    Assunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnviadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EnviadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailEnvios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailListas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Destinatarios = table.Column<string>(type: "character varying(13000)", maxLength: 13000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailListas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailModelos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Remetente = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Assunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Corpo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ListaId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailModelos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailModelos_EmailListas_ListaId",
                        column: x => x.ListaId,
                        principalTable: "EmailListas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailEnvios_ControloId",
                table: "EmailEnvios",
                column: "ControloId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailListas_Nome",
                table: "EmailListas",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailModelos_ListaId",
                table: "EmailModelos",
                column: "ListaId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailModelos_Nome",
                table: "EmailModelos",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailEnvios");

            migrationBuilder.DropTable(
                name: "EmailModelos");

            migrationBuilder.DropTable(
                name: "EmailListas");
        }
    }
}
