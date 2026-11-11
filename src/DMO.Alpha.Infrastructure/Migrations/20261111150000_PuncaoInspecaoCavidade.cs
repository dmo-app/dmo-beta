using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>DMO-753 (Diogo, 11/11/2026): a cavidade de cada punção (A1, B1, C1…), escrita por quem preenche.</summary>
    public partial class PuncaoInspecaoCavidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cavidade",
                table: "PuncaoInspecoes",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Cavidade", table: "PuncaoInspecoes");
        }
    }
}
