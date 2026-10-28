using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <summary>
    /// Os AN das Boquilhas (Diogo, 28/10/2026: «vamos passar a registar AN»):
    /// a quantidade de AN do lote na ferramenta BQ e os AN de cada movimento.
    /// Só acrescenta colunas vazias; nada se apaga nem muda.
    /// </summary>
    public partial class AnDasBoquilhas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnQuantity",
                table: "Tools",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnQuantity",
                table: "BqMovements",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnQuantity",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "AnQuantity",
                table: "BqMovements");
        }
    }
}
