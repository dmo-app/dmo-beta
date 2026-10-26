using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMO.Alpha.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArmazemOcupacaoVariasPosicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArmazemOcupacoes_ToolId_Ativa",
                table: "ArmazemOcupacoes");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemOcupacoes_ToolId_PosicaoId_Ativa",
                table: "ArmazemOcupacoes",
                columns: new[] { "ToolId", "PosicaoId" },
                unique: true,
                filter: "\"LibertadaEmUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArmazemOcupacoes_ToolId_PosicaoId_Ativa",
                table: "ArmazemOcupacoes");

            migrationBuilder.CreateIndex(
                name: "IX_ArmazemOcupacoes_ToolId_Ativa",
                table: "ArmazemOcupacoes",
                column: "ToolId",
                unique: true,
                filter: "\"LibertadaEmUtc\" IS NULL");
        }
    }
}
