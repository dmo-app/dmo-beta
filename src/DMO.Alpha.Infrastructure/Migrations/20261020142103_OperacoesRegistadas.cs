using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OperacoesRegistadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperacoesRegistadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultadoTipo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultadoId = table.Column<int>(type: "integer", nullable: true),
                    MomentoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesRegistadas", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperacoesRegistadas");
        }
    }
}
