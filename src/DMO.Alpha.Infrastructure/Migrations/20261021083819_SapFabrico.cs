using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SapFabrico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SapFabricos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Material = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Linha = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InicioEm = table.Column<DateOnly>(type: "date", nullable: false),
                    FimEm = table.Column<DateOnly>(type: "date", nullable: false),
                    ProducaoBruta = table.Column<long>(type: "bigint", nullable: true),
                    ProducaoLiquida = table.Column<long>(type: "bigint", nullable: true),
                    LidoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LidoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    LidoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ficheiro = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    FicheiroModificadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapFabricos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SapFabricos_Users_LidoPorUserId",
                        column: x => x.LidoPorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SapFabricos_LidoPorUserId",
                table: "SapFabricos",
                column: "LidoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SapFabricos_Linha_InicioEm",
                table: "SapFabricos",
                columns: new[] { "Linha", "InicioEm" });

            migrationBuilder.CreateIndex(
                name: "IX_SapFabricos_Material_Linha_InicioEm_FimEm",
                table: "SapFabricos",
                columns: new[] { "Material", "Linha", "InicioEm", "FimEm" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SapFabricos");
        }
    }
}
