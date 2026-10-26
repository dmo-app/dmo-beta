using System;
using DMO.Alpha.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>A imagem do artigo por referência completa. Só acrescenta uma tabela.</summary>
    [DbContext(typeof(DmoDbContext))]
    [Migration("20261026150000_JobOnArtigoImagem")]
    public partial class JobOnArtigoImagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobOnArtigoImagens",
                columns: table => new
                {
                    ReferenciaTotal = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Dados = table.Column<byte[]>(type: "bytea", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoPor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOnArtigoImagens", x => x.ReferenciaTotal);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobOnArtigoImagens");
        }
    }
}
